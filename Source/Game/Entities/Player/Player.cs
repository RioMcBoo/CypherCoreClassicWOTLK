﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Framework.Dynamic;
using Game.Achievements;
using Game.AI;
using Game.Arenas;
using Game.BattleFields;
using Game.BattleGrounds;
using Game.BattlePets;
using Game.Chat;
using Game.DataStorage;
using Game.Groups;
using Game.Guilds;
using Game.Loots;
using Game.Mails;
using Game.Maps;
using Game.Misc;
using Game.Movement;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using static Global;

namespace Game.Entities
{
    public partial class Player : Unit
    {
        public Player(WorldSession session) : base(true)
        {
            ObjectTypeMask |= TypeMask.Player;
            ObjectTypeId = TypeId.Player;

            m_playerData = new PlayerData();
            m_activePlayerData = new ActivePlayerData();

            _session = session;

            // players always accept
            if (!GetSession().HasPermission(RBACPermissions.CanFilterWhispers))
                SetAcceptWhispers(true);

            m_regenInterruptTimestamp = LoopTime.ServerTime;

            m_zoneUpdateId = -1;
            m_nextSave = WorldConfig.Values[WorldCfg.IntervalSave].Milliseconds;
            m_customizationsChanged = false;

            SetGroupInvite(null);

            atLoginFlags = AtLoginFlags.None;
            PlayerTalkClass = new PlayerMenu(session);
            m_currentBuybackSlot = InventorySlots.BuyBackStart;

            for (byte i = 0; i < (int)MirrorTimerType.Max; i++)
                m_MirrorTimer[i] = new(-1);

            m_logintime = LoopTime.ServerTime;
            m_Last_tick = m_logintime;

            m_dungeonDifficulty = Difficulty.Normal;
            m_raidDifficulty = Difficulty.NormalRaid;
            m_legacyRaidDifficulty = Difficulty.Raid10N;
            m_InstanceValid = true;

            _specializationInfo = new SpecializationInfo();

            for (byte i = 0; i < (byte)BaseModGroup.End; ++i)
            {
                m_auraBaseFlatMod[i] = 0.0f;
                m_auraBasePctMod[i] = 1.0f;
            }

            for (var i = 0; i < (int)SpellModOp.Max; ++i)
            {
                m_spellMods[i] = new List<SpellModifier>[(int)SpellModType.End];

                for (var c = 0; c < (int)SpellModType.End; ++c)
                    m_spellMods[i][c] = new List<SpellModifier>();
            }

            // Honor System
            m_lastHonorUpdateTime = LoopTime.RealmTime;

            m_unitMovedByMe = this;
            m_playerMovingMe = this;
            seerView = this;

            m_isActive = true;
            m_ControlledByPlayer = true;

            WorldMgr.IncreasePlayerCount();

            _cinematicMgr = new CinematicManager(this);

            m_achievementSys = new PlayerAchievementMgr(this);
            reputationMgr = new ReputationMgr(this);
            m_questObjectiveCriteriaMgr = new QuestObjectiveCriteriaManager(this);
            m_sceneMgr = new SceneMgr(this);

            for (var i = 0; i < SharedConst.MaxPlayerBGQueues; ++i)
                m_bgBattlegroundQueueID[i] = new BgBattlegroundQueueID_Rec();

            m_bgData = new BGData();

            _restMgr = new RestMgr(this);

            m_groupUpdateTimer = new((Seconds)5);
        }

        public override void Dispose()
        {
            // Note: buy back item already deleted from DB when player was saved
            for (byte i = 0; i < (int)PlayerSlots.Count; ++i)
            {
                if (m_items[i] != null)
                    m_items[i].Dispose();
            }

            m_spells.Clear();
            _specializationInfo = null;
            m_mail.Clear();

            foreach (var item in mMitems.Values)
                item.Dispose();

            PlayerTalkClass.ClearMenus();
            ItemSetEff.Clear();

            _declinedname = null;
            Runes = null;
            m_achievementSys = null;
            reputationMgr = null;

            _cinematicMgr.Dispose();

            for (byte i = 0; i < SharedConst.VoidStorageMaxSlot; ++i)
                _voidStorageItems[i] = null;

            ClearResurrectRequestData();

            WorldMgr.DecreasePlayerCount();

            base.Dispose();
        }

        //Core
        public bool Create(long guidlow, CharacterCreateInfo createInfo)
        {
            _Create(ObjectGuid.Create(HighGuid.Player, guidlow));

            SetName(createInfo.Name);

            PlayerInfo info = ObjectMgr.GetPlayerInfo(createInfo.RaceId, createInfo.ClassId);
            if (info == null)
            {
                Log.outError(LogFilter.Player, 
                    $"PlayerCreate: Possible hacking-attempt:" +
                    $" Account {GetSession().GetAccountId()} tried creating a character named '{GetName()}' " +
                    $"with an invalid race/class pair ({createInfo.RaceId}/{createInfo.ClassId}) - refusing to do so.");
                return false;
            }

            var cEntry = CliDB.ChrClassesStorage.LookupByKey((int)createInfo.ClassId);
            if (cEntry == null)
            {
                Log.outError(LogFilter.Player, 
                    $"PlayerCreate: Possible hacking-attempt: " +
                    $"Account {GetSession().GetAccountId()} tried creating a character named '{GetName()}' " +
                    $"with an invalid character class ({createInfo.ClassId}) - refusing to do so (wrong DBC-files?)");
                return false;
            }

            if (!GetSession().ValidateAppearance(createInfo.RaceId, createInfo.ClassId, createInfo.Sex, createInfo.Customizations))
            {
                Log.outError(LogFilter.Player, 
                    $"Player.Create: Possible hacking-attempt: " +
                    $"Account {GetSession().GetAccountId()} tried creating a character named '{GetName()}' " +
                    $"with invalid appearance attributes - refusing to do so.");
                return false;
            }

            var position = createInfo.UseNPE && info.createPositionNPE.HasValue ? info.createPositionNPE.Value : info.createPosition;

            m_createTime = LoopTime.ServerTime;
            m_createMode = createInfo.UseNPE && info.createPositionNPE.HasValue ? PlayerCreateMode.NPE : PlayerCreateMode.Normal;

            Relocate(position.Loc);

            SetMap(MapMgr.CreateMap(position.Loc.GetMapId(), this));

            if (position.TransportGuid.HasValue)
            {
                Transport transport = ObjectAccessor.GetTransport(this, ObjectGuid.Create(HighGuid.Transport, position.TransportGuid.Value));
                if (transport != null)
                {
                    transport.AddPassenger(this);
                    m_movementInfo.transport.pos.Relocate(position.Loc);
                    position.Loc.GetPosition(out float x, out float y, out float z, out float o);
                    transport.CalculatePassengerPosition(ref x, ref y, ref z, ref o);
                    Relocate(x, y, z, o);
                }
            }

            // set initial homebind position
            SetHomebind(this, GetAreaId());

            PowerType powertype = cEntry.DisplayPower;

            SetObjectScale(1.0f);

            SetFactionForRace(createInfo.RaceId);

            if (!IsValidGender(createInfo.Sex))
            {
                Log.outError(LogFilter.Player, 
                    $"Player:Create: Possible hacking-attempt: Account {GetSession().GetAccountId()} " +
                    $"tried creating a character named '{GetName()}' " +
                    $"with an invalid gender ({createInfo.Sex}) - refusing to do so.");
                return false;
            }

            SetRace(createInfo.RaceId);
            SetClass(createInfo.ClassId);
            SetGender(createInfo.Sex);
            SetPowerType(powertype, false);
            InitDisplayIds();
            if ((RealmType)WorldConfig.Values[WorldCfg.GameType].Int32 == RealmType.PVP 
                || (RealmType)WorldConfig.Values[WorldCfg.GameType].Int32 == RealmType.RPPVP)
            {
                SetPvpFlag(UnitPVPStateFlags.PvP);
                SetUnitFlag(UnitFlags.PlayerControlled);
            }

            SetUnitFlag2(UnitFlags2.RegeneratePower);

            SetWatchedFactionIndex(-1);

            SetCustomizations(createInfo.Customizations);
            SetRestState(RestTypes.XP, ((GetSession().IsARecruiter() || GetSession().GetRecruiterId() != 0) ? PlayerRestState.RAFLinked : PlayerRestState.Normal));
            SetRestState(RestTypes.Honor, PlayerRestState.Normal);
            SetNativeGender(createInfo.Sex);
            SetInventorySlotCount(InventorySlots.DefaultSize);

            // set starting level
            SetLevel(GetStartLevel(createInfo.RaceId, createInfo.ClassId, createInfo.TemplateSet));

            InitRunes();

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Coinage), WorldConfig.Values[WorldCfg.StartPlayerMoney].Int32);

            // Played time
            m_Last_tick = LoopTime.ServerTime;
            m_PlayedTimeTotal = Seconds.Zero;
            m_PlayedTimeLevel = Seconds.Zero;

            // base stats and related field values
            InitStatsForLevel();
            InitTaxiNodesForLevel();
            InitGlyphsForLevel();
            InitTalentForLevel();
            InitializeSkillFields();
            InitPrimaryProfessions();                               // to max set before any spell added

            // apply original stats mods before spell loading or item equipment that call before equip _RemoveStatsMods()
            UpdateMaxHealth();                                      // Update max Health (for add bonus from stamina)
            SetFullHealth();
            SetFullPower(PowerType.Mana);

            // original spells
            LearnDefaultSkills();
            LearnCustomSpells();

            // Original action bar. Do not use Player.AddActionButton because we do not have skill spells loaded at this time
            // but checks will still be performed later when loading character from db in Player._LoadActions
            foreach (var action in info.action)
            {
                // create new button
                ActionButton ab = new();

                // set data
                ab.SetActionAndType(action.action, (ActionButtonType)action.type);

                m_actionButtons[action.button] = ab;
            }

            // original items
            foreach (PlayerCreateInfoItem initialItem in info.item)
                StoreNewItemInBestSlots(initialItem.item_id, initialItem.item_amount, info.itemContext);

            // bags and main-hand weapon must equipped at this moment
            // now second pass for not equipped (offhand weapon/shield if it attempt equipped before main-hand weapon)
            int inventoryEnd = InventorySlots.ItemStart + GetInventorySlotCount();
            for (byte i = InventorySlots.ItemStart; i < inventoryEnd; i++)
            {
                Item pItem = GetItemByPos(i);
                if (pItem != null)
                {
                    List<ItemPosCount> destList;
                    // equip offhand weapon/shield if it attempt equipped before main-hand weapon
                    InventoryResult msg = CanEquipItem(ItemSlot.Null, out destList, pItem, false);
                    if (msg == InventoryResult.Ok)
                    {
                        RemoveItem(new(i), true);
                        EquipItem(destList, pItem, true);
                    }
                    // move other items to more appropriate slots
                    else
                    {
                        msg = CanStoreItem(ItemPos.Undefined, out destList, pItem);
                        if (msg == InventoryResult.Ok)
                        {
                            RemoveItem(new(i), true);
                            StoreItem(destList, pItem, true);
                        }
                    }
                }
            }

            // all item positions
            SetActiveTalentGroup(0);

            GetThreatManager().Initialize();

            return true;
        }

        public override void Update(TimeSpan diff)
        {
            if (!IsInWorld)
                return;

            // undelivered mail
            if (m_nextMailDelivereTime != ServerTime.Zero && m_nextMailDelivereTime <= LoopTime.ServerTime)
            {
                SendNewMail();
                ++unReadMails;

                // It will be recalculate at mailbox open (for unReadMails important non-0 until mailbox open, it also will be recalculated)
                m_nextMailDelivereTime = ServerTime.Zero;
            }

            // Update cinematic location, if 500ms have passed and we're doing a cinematic now.
            _cinematicMgr.m_cinematicDiff += diff;
            if (_cinematicMgr.m_cinematicCamera != null && _cinematicMgr.m_activeCinematic != null && Time.Diff(_cinematicMgr.m_lastCinematicCheck) > (Milliseconds)500)
            {
                _cinematicMgr.m_lastCinematicCheck = LoopTime.ServerTime;
                _cinematicMgr.UpdateCinematicLocation(diff);
            }

            //used to implement delayed far teleports
            SetCanDelayTeleport(true);
            base.Update(diff);
            SetCanDelayTeleport(false);

            // Unit::Update updates the spell history and spell states. We can now check if we can launch another pending cast.
            if (CanExecutePendingSpellCastRequest())
                ExecutePendingSpellCastRequest();

            ServerTime now = LoopTime.ServerTime;

            UpdatePvPFlag(now);

            UpdateContestedPvP(diff);

            UpdateDuelFlag(now);

            CheckDuelDistance(now);

            UpdateAfkReport(now);

            if (GetCombatManager().HasPvPCombat()) // Only set when in pvp combat
            {
                Aura aura = GetAura(PlayerConst.SpellPvpRulesEnabled);
                if (aura != null)
                {
                    if (!aura.IsPermanent())
                        aura.SetDuration(aura.GetSpellInfo().GetMaxDuration());
                }
            }

            AIUpdateTick(diff);

            // Update items that have just a limited lifetime
            TimeSpan elapsed = now - m_Last_tick;
            if (now > m_Last_tick)
                UpdateItemDuration((Seconds)elapsed);

            // check every second
            if (now > m_Last_tick + (Seconds)1)
                UpdateSoulboundTradeItems();

            // If mute expired, remove it from the DB
            if (GetSession().m_mutedUntilTime != ServerTime.Zero && GetSession().m_mutedUntilTime < now)
            {
                GetSession().m_mutedUntilTime = ServerTime.Zero;
                PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_MUTE_TIME);
                stmt.SetInt32(0, 0); // Set the mute time to 0
                stmt.SetString(1, "");
                stmt.SetString(2, "");
                stmt.SetInt32(3, GetSession().GetAccountId());
                DB.Login.Execute(stmt);
            }

            var timedQuests = GetTimedQuests();
            if (!timedQuests.Empty())
            {
                foreach (var id in timedQuests)
                {
                    QuestStatusData q_status = m_QuestStatus[id];
                    if (q_status.Timer <= diff)
                        FailQuest(id);
                    else
                    {
                        q_status.Timer -= diff;
                        m_QuestStatusSave[id] = QuestSaveType.Default;
                    }
                }
            }

            m_achievementSys.UpdateTimedCriteria(diff);

            DoMeleeAttackIfReady();

            _restMgr.Update();

            if (m_weaponChangeTimer > 0)
            {
                if (diff >= m_weaponChangeTimer)
                    m_weaponChangeTimer = Milliseconds.Zero;
                else
                    m_weaponChangeTimer -= diff;
            }

            if (m_zoneUpdateTimer > 0)
            {
                if (diff >= m_zoneUpdateTimer)
                {
                    // On zone update tick check if we are still in an inn if we are supposed to be in one
                    if (_restMgr.HasRestFlag(RestFlag.Tavern))
                    {
                        AreaTriggerRecord atEntry = CliDB.AreaTriggerStorage.LookupByKey(_restMgr.GetInnTriggerId());
                        if (atEntry == null || !IsInAreaTriggerRadius(atEntry))
                            _restMgr.RemoveRestFlag(RestFlag.Tavern);
                    }

                    int newzone, newarea;
                    GetZoneAndAreaId(out newzone, out newarea);

                    if (m_zoneUpdateId != newzone)
                        UpdateZone(newzone, newarea);                // also update area
                    else
                    {
                        // use area updates as well
                        // needed for free far all arenas for example
                        if (m_areaUpdateId != newarea)
                            UpdateArea(newarea);

                        m_zoneUpdateTimer = (Seconds)1;
                    }
                }
                else
                    m_zoneUpdateTimer -= diff;
            }

            if (IsAlive())
            {
                RegenTimer += diff;
                RegenerateAll();
            }

            if (m_deathState == DeathState.JustDied)
                KillPlayer();

            if (m_nextSave > 0)
            {
                if (diff >= m_nextSave)
                {
                    // m_nextSave reset in SaveToDB call
                    ScriptMgr.OnPlayerSave(this);
                    SaveToDB();
                    Log.outDebug(LogFilter.Player, $"Player '{GetName()}' (GUID: {GetGUID()}) saved");
                }
                else
                    m_nextSave -= diff;
            }

            //Handle Water/drowning
            HandleDrowning((Milliseconds)diff);

            // Played time
            if (now > m_Last_tick)
            {                
                m_PlayedTimeTotal += elapsed;
                m_PlayedTimeLevel += elapsed;
                m_Last_tick = now;
            }

            if (GetDrunkValue() != 0)
            {
                m_drunkTimer += diff;
                if (m_drunkTimer > (Seconds)9)
                    HandleSobering();
            }
            if (HasPendingBind())
            {
                if (_pendingBindTimer <= diff)
                {
                    // Player left the instance
                    if (_pendingBindId == GetInstanceId())
                        ConfirmPendingBind();
                    SetPendingBind(0, Milliseconds.Zero);
                }
                else
                    _pendingBindTimer -= diff;
            }
            // not auto-free ghost from body in instances
            if (m_deathTimer > 0 && !GetMap().Instanceable() && !HasAuraType(AuraType.PreventResurrection))
            {
                if (diff >= m_deathTimer)
                {
                    m_deathTimer = Milliseconds.Zero;
                    BuildPlayerRepop();
                    RepopAtGraveyard();
                }
                else
                    m_deathTimer -= diff;
            }

            UpdateEnchantTime((Milliseconds)diff);
            UpdateHomebindTime((Milliseconds)diff);

            if (!_instanceResetTimes.Empty())
            {
                foreach (var instance in _instanceResetTimes.ToList())
                {
                    if (instance.Value < now)
                        _instanceResetTimes.Remove(instance.Key);
                }
            }

            // group update
            m_groupUpdateTimer.Update(diff);
            if (m_groupUpdateTimer.Passed())
            {
                SendUpdateToOutOfRangeGroupMembers();
                m_groupUpdateTimer.Reset((Seconds)5);
            }

            Pet pet = GetPet();
            if (pet != null && !pet.IsWithinDistInMap(this, GetMap().GetVisibilityRange()) && !pet.IsPossessed())
                RemovePet(pet, PetSaveMode.NotInSlot, true);

            if (IsAlive())
            {
                if (m_hostileReferenceCheckTimer <= diff)
                {
                    m_hostileReferenceCheckTimer = (Seconds)15;
                    if (!GetMap().IsDungeon())
                        GetCombatManager().EndCombatBeyondRange(GetVisibilityRange(), true);
                }
                else
                    m_hostileReferenceCheckTimer -= diff;
            }

            //we should execute delayed teleports only for alive(!) players
            //because we don't want player's ghost teleported from graveyard
            if (IsHasDelayedTeleport() && IsAlive())
                TeleportTo(teleportDest, m_teleport_options);
        }

        public override void SetDeathState(DeathState s)
        {
            bool oldIsAlive = IsAlive();

            if (s == DeathState.JustDied)
            {
                if (!oldIsAlive)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player.setDeathState: Attempted to kill a dead player '{GetName()}' ({GetGUID()})");
                    return;
                }

                // clear all pending spell cast requests when dying
                CancelPendingCastRequest();

                // drunken state is cleared on death
                SetDrunkValue(0);

                SetPower(PowerType.ComboPoints, 0);

                ClearResurrectRequestData();

                //FIXME: is pet dismissed at dying or releasing spirit? if second, add setDeathState(DEAD) to HandleRepopRequestOpcode and define pet unsummon here with (s == DEAD)
                RemovePet(null, PetSaveMode.NotInSlot, true);

                InitializeSelfResurrectionSpells();

                FailQuestsWithFlag(QuestFlags.CompletionNoDeath);

                UpdateCriteria(CriteriaType.DieOnMap, 1);
                UpdateCriteria(CriteriaType.DieAnywhere, 1);
                UpdateCriteria(CriteriaType.DieInInstance, 1);

                // reset all death criterias
                FailCriteria(CriteriaFailEvent.Death, 0);
            }

            base.SetDeathState(s);

            if (IsAlive() && !oldIsAlive)
                //clear aura case after resurrection by another way (spells will be applied before next death)
                ClearSelfResSpell();
        }

        public override void DestroyForPlayer(Player target)
        {
            base.DestroyForPlayer(target);

            if (target == this)
            {
                for (byte i = EquipmentSlot.Start; i < InventorySlots.BankBagEnd; ++i)
                {
                    if (m_items[i] == null)
                        continue;

                    m_items[i].DestroyForPlayer(target);
                }

                for (byte i = InventorySlots.KeyringStart; i < InventorySlots.ChildEquipmentEnd; ++i)
                {
                    if (m_items[i] == null)
                        continue;

                    m_items[i].DestroyForPlayer(target);
                }
            }
        }

        public override void CleanupsBeforeDelete(bool finalCleanup = true)
        {
            TradeCancel(false);
            DuelComplete(DuelCompleteType.Interrupted);

            base.CleanupsBeforeDelete(finalCleanup);
        }

        public override void AddToWorld()
        {
            // Do not add/remove the player from the object storage
            // It will crash when updating the ObjectAccessor
            // The player should only be added when logging in
            base.AddToWorld();

            for (byte i = (int)PlayerSlots.Start; i < (int)PlayerSlots.End; ++i)
                if (m_items[i] != null)
                    m_items[i].AddToWorld();
        }

        public override void RemoveFromWorld()
        {
            // cleanup
            if (IsInWorld)
            {
                // Release charmed creatures, unsummon totems and remove pets/guardians
                StopCastingCharm();
                StopCastingBindSight();
                UnsummonPetTemporaryIfAny();
                SetPower(PowerType.ComboPoints, 0);
                GetSession().DoLootReleaseAll();
                m_lootRolls.Clear();
                OutdoorPvPMgr.HandlePlayerLeaveZone(this, m_zoneUpdateId);
                BattleFieldMgr.HandlePlayerLeaveZone(this, m_zoneUpdateId);
            }

            // Remove items from world before self - player must be found in Item.RemoveFromObjectUpdate
            for (byte i = (int)PlayerSlots.Start; i < (int)PlayerSlots.End; ++i)
            {
                if (m_items[i] != null)
                    m_items[i].RemoveFromWorld();
            }

            // Do not add/remove the player from the object storage
            // It will crash when updating the ObjectAccessor
            // The player should only be removed when logging out
            base.RemoveFromWorld();

            WorldObject viewpoint = GetViewpoint();
            if (viewpoint != null)
            {
                Log.outError(LogFilter.Player,
                    $"Player {GetName()} has viewpoint {viewpoint.GetEntry()} {viewpoint.GetTypeId()} " +
                    $"when removed from world");
                SetViewpoint(viewpoint, false);
            }
        }

        public void SetTransportServerTime(RelativeTime transportServerTime) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TransportServerTime), transportServerTime); 
        }

        void ScheduleDelayedOperation(PlayerDelayedOperations operation)
        {
            if (operation < PlayerDelayedOperations.End)
                m_DelayedOperations |= operation;
        }

        public void ProcessDelayedOperations()
        {
            if (m_DelayedOperations == 0)
                return;

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.ResurrectPlayer))
                ResurrectUsingRequestDataImpl();

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.SavePlayer))
                SaveToDB();

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.SpellCastDeserter))
                CastSpell(this, 26013, true);               // Deserter

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.BGMountRestore))
            {
                if (m_bgData.mountSpell != 0)
                {
                    CastSpell(this, m_bgData.mountSpell, true);
                    m_bgData.mountSpell = 0;
                }
            }

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.BGTaxiRestore))
            {
                if (m_bgData.HasTaxiPath())
                {
                    m_taxi.AddTaxiDestination(m_bgData.taxiPath[0]);
                    m_taxi.AddTaxiDestination(m_bgData.taxiPath[1]);
                    m_bgData.ClearTaxiPath();

                    ContinueTaxiFlight();
                }
            }

            if (m_DelayedOperations.HasAnyFlag(PlayerDelayedOperations.BGGroupRestore))
            {
                Group g = GetGroup();
                if (g != null)
                    g.SendUpdateToPlayer(GetGUID());
            }

            //we have executed ALL delayed ops, so clear the flag
            m_DelayedOperations = 0;
        }

        public override bool IsLoading()
        {
            return GetSession().PlayerLoading();
        }

        new PlayerAI GetAI() { return (PlayerAI)i_AI; }

        //Network
        public void SendPacket(ServerPacket data)
        {
            _session.SendPacket(data);
        }

        public DeclinedName GetDeclinedNames() { return _declinedname; }

        public SceneMgr GetSceneMgr() { return m_sceneMgr; }

        public RestMgr GetRestMgr() { return _restMgr; }

        public bool IsAdvancedCombatLoggingEnabled() { return _advancedCombatLoggingEnabled; }
        public void SetAdvancedCombatLogging(bool enabled) { _advancedCombatLoggingEnabled = enabled; }

        public void SetInvSlot(int slot, ObjectGuid guid) { SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.InvSlots, slot), guid); }

        //Taxi
        public void InitTaxiNodesForLevel() { m_taxi.InitTaxiNodesForLevel(GetRace(), GetClass(), GetLevel()); }

        //Cheat Commands
        public bool GetCommandStatus(PlayerCommandStates command) { return (_activeCheats & command) != 0; }
        public void SetCommandStatusOn(PlayerCommandStates command) { _activeCheats |= command; }
        public void SetCommandStatusOff(PlayerCommandStates command) { _activeCheats &= ~command; }

        //Pet - Summons - Vehicles
        public PetStable GetPetStable() { return m_petStable; }

        public PetStable GetOrInitPetStable()
        {
            if (m_petStable == null)
                m_petStable = new();

            return m_petStable;
        }

        public void AddPetToUpdateFields(PetStable.PetInfo pet, PetSaveMode slot, PetStableFlags flags)
        {
            StableInfo ufStable = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetStable);
            StablePetInfo ufPet = new();
            ufPet.ModifyValue(ufPet.PetSlot).SetValue((int)slot);
            ufPet.ModifyValue(ufPet.PetNumber).SetValue(pet.PetNumber);
            ufPet.ModifyValue(ufPet.CreatureID).SetValue(pet.CreatureId);
            ufPet.ModifyValue(ufPet.DisplayID).SetValue(pet.DisplayId);
            ufPet.ModifyValue(ufPet.ExperienceLevel).SetValue(pet.Level);
            ufPet.ModifyValue(ufPet.PetFlags).SetValue((byte)flags);
            ufPet.ModifyValue(ufPet.Name).SetValue(pet.Name);
            AddDynamicUpdateFieldValue(ufStable.ModifyValue(ufStable.Pets), ufPet);
        }

        public void SetPetSlot(int petNumber, PetSaveMode dstPetSlot)
        {
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Interacting);

            WorldSession sess = GetSession();
            PetStable petStable = GetPetStable();
            if (petStable == null)
            {
                sess.SendPetStableResult(StableResult.InternalError);
                return;
            }

            var (srcPet, srcPetSlot) = Pet.GetLoadPetInfo(petStable, 0, petNumber, null);
            PetStable.PetInfo dstPet = Pet.GetLoadPetInfo(petStable, 0, 0, dstPetSlot).Item1;

            if (srcPet == null || srcPet.Type != PetType.Hunter)
            {
                sess.SendPetStableResult(StableResult.InternalError);
                return;
            }

            if (dstPet != null && dstPet.Type != PetType.Hunter)
            {
                sess.SendPetStableResult(StableResult.InternalError);
                return;
            }

            PetStable.PetInfo src = null;
            PetStable.PetInfo dst = null;
            int? newActivePetIndex = null;

            if (SharedConst.IsActivePetSlot(srcPetSlot) && SharedConst.IsActivePetSlot(dstPetSlot))
            {
                // active<.active: only swap ActivePets and CurrentPetIndex (do not despawn pets)
                src = petStable.ActivePets[srcPetSlot - PetSaveMode.FirstActiveSlot];
                dst = petStable.ActivePets[dstPetSlot - PetSaveMode.FirstActiveSlot];

                if (petStable.GetCurrentActivePetIndex() == (uint)srcPetSlot)
                    newActivePetIndex = (int)dstPetSlot;
                else if (petStable.GetCurrentActivePetIndex() == (uint)dstPetSlot)
                    newActivePetIndex = (int)srcPetSlot;
            }
            else if (SharedConst.IsStabledPetSlot(srcPetSlot) && SharedConst.IsStabledPetSlot(dstPetSlot))
            {
                // stabled<.stabled: only swap StabledPets
                src = petStable.StabledPets[srcPetSlot - PetSaveMode.FirstStableSlot];
                dst = petStable.StabledPets[dstPetSlot - PetSaveMode.FirstStableSlot];
            }
            else if (SharedConst.IsActivePetSlot(srcPetSlot) && SharedConst.IsStabledPetSlot(dstPetSlot))
            {
                // active<.stabled: swap petStable contents and despawn active pet if it is involved in swap
                if (petStable.CurrentPetIndex == (uint)srcPetSlot)
                {
                    Pet oldPet = GetPet();
                    if (oldPet != null && !oldPet.IsAlive())
                    {
                        sess.SendPetStableResult(StableResult.InternalError);
                        return;
                    }

                    RemovePet(oldPet, PetSaveMode.NotInSlot);
                }

                if (dstPet != null)
                {
                    CreatureTemplate creatureInfo = Global.ObjectMgr.GetCreatureTemplate(dstPet.CreatureId);
                    if (creatureInfo == null || !creatureInfo.IsTameable(CanTameExoticPets(), creatureInfo.GetDifficulty(Difficulty.None)))
                    {
                        sess.SendPetStableResult(StableResult.CantControlExotic);
                        return;
                    }
                }

                src = petStable.ActivePets[srcPetSlot - PetSaveMode.FirstActiveSlot];
                dst = petStable.StabledPets[dstPetSlot - PetSaveMode.FirstStableSlot];
            }
            else if (SharedConst.IsStabledPetSlot(srcPetSlot) && SharedConst.IsActivePetSlot(dstPetSlot))
            {
                // stabled<.active: swap petStable contents and despawn active pet if it is involved in swap
                if (petStable.CurrentPetIndex == (uint)dstPetSlot)
                {
                    Pet oldPet = GetPet();
                    if (oldPet != null && !oldPet.IsAlive())
                    {
                        sess.SendPetStableResult(StableResult.InternalError);
                        return;
                    }

                    RemovePet(oldPet, PetSaveMode.NotInSlot);
                }

                CreatureTemplate creatureInfo = Global.ObjectMgr.GetCreatureTemplate(srcPet.CreatureId);
                if (creatureInfo == null || !creatureInfo.IsTameable(CanTameExoticPets(), creatureInfo.GetDifficulty(Difficulty.None)))
                {
                    sess.SendPetStableResult(StableResult.CantControlExotic);
                    return;
                }

                src = petStable.StabledPets[srcPetSlot - PetSaveMode.FirstStableSlot];
                dst = petStable.ActivePets[dstPetSlot - PetSaveMode.FirstActiveSlot];
            }

            SQLTransaction trans = new SQLTransaction();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_PET_SLOT_BY_ID);
            stmt.SetInt16(0, (short)dstPetSlot);
            stmt.SetInt64(1, GetGUID().GetCounter());
            stmt.SetInt32(2, srcPet.PetNumber);
            trans.Append(stmt);

            if (dstPet != null)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_PET_SLOT_BY_ID);
                stmt.SetInt16(0, (short)srcPetSlot);
                stmt.SetInt64(1, GetGUID().GetCounter());
                stmt.SetInt32(2, dstPet.PetNumber);
                trans.Append(stmt);
            }


            GetSession().AddTransactionCallback(DB.Characters.AsyncCommitTransaction(trans)).AfterComplete(success =>
            {
                if (sess.GetPlayer() == this)
                {
                    if (success)
                    {
                        Extensions.Swap(ref src, ref dst);
                        if (newActivePetIndex.HasValue)
                            sess.GetPlayer().GetPetStable().SetCurrentActivePetIndex(newActivePetIndex.Value);

                        int srcPetIndex = m_activePlayerData.PetStable.GetValue().Pets.FindIndexIf(p => p.PetSlot == (int)srcPetSlot);
                        int dstPetIndex = m_activePlayerData.PetStable.GetValue().Pets.FindIndexIf(p => p.PetSlot == (int)dstPetSlot);

                        if (srcPetIndex >= 0)
                        {
                            StableInfo stableInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetStable);
                            StablePetInfo stablePetInfo = stableInfo.ModifyValue(stableInfo.Pets, srcPetIndex);
                            SetUpdateFieldValue(stablePetInfo.ModifyValue(stablePetInfo.PetSlot), (int)dstPetSlot);
                        }

                        if (dstPetIndex >= 0)
                        {
                            StableInfo stableInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetStable);
                            StablePetInfo stablePetInfo = stableInfo.ModifyValue(stableInfo.Pets, dstPetIndex);
                            SetUpdateFieldValue(stablePetInfo.ModifyValue(stablePetInfo.PetSlot), (int)srcPetSlot);
                        }

                        sess.SendPetStableResult(StableResult.StableSuccess);
                    }
                    else
                    {
                        sess.SendPetStableResult(StableResult.InternalError);
                    }
                }
            });
        }

        public ObjectGuid GetStableMaster()
        {
            if (!m_activePlayerData.PetStable.HasValue())
                return ObjectGuid.Empty;

            return m_activePlayerData.PetStable.GetValue().StableMaster;
        }

        public void SetStableMaster(ObjectGuid stableMaster)
        {
            if (!m_activePlayerData.PetStable.HasValue())
                return;

            StableInfo stableInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetStable);
            SetUpdateFieldValue(stableInfo.ModifyValue(stableInfo.StableMaster), stableMaster);
        }

        // last used pet number (for BG's)
        public int GetLastPetNumber() { return m_lastpetnumber; }
        public void SetLastPetNumber(int petnumber) { m_lastpetnumber = petnumber; }
        public int GetTemporaryUnsummonedPetNumber() { return m_temporaryUnsummonedPetNumber; }
        public void SetTemporaryUnsummonedPetNumber(int petnumber) { m_temporaryUnsummonedPetNumber = petnumber; }

        public ReactStates? GetTemporaryPetReactState() { return m_temporaryPetReactState; }

        public void DisablePetControlsOnMount(ReactStates reactState, CommandStates commandState)
        {
            Pet pet = GetPet();
            if (pet == null)
                return;

            m_temporaryPetReactState = pet.GetReactState();
            pet.SetReactState(reactState);
            CharmInfo charmInfo = pet.GetCharmInfo();
            if (charmInfo != null)
                charmInfo.SetCommandState(commandState);

            pet.GetMotionMaster().MoveFollow(this, SharedConst.PetFollowDist, pet.GetFollowAngle());

            PetMode petMode = new();
            petMode.PetGUID = pet.GetGUID();
            petMode.ReactState = reactState;
            petMode.CommandState = commandState;
            petMode.Flag = 0;
            SendPacket(petMode);
        }

        public void EnablePetControlsOnDismount()
        {
            Pet pet = GetPet();
            if (pet != null)
            {
                PetMode petMode = new();
                petMode.PetGUID = pet.GetGUID();
                if (m_temporaryPetReactState.HasValue)
                {
                    petMode.ReactState = m_temporaryPetReactState.Value;
                    pet.SetReactState(m_temporaryPetReactState.Value);
                }

                CharmInfo charmInfo = pet.GetCharmInfo();
                if (charmInfo != null)
                    petMode.CommandState = charmInfo.GetCommandState();

                petMode.Flag = 0;
                SendPacket(petMode);
            }

            m_temporaryPetReactState = null;
        }

        public void UnsummonPetTemporaryIfAny()
        {
            Pet pet = GetPet();
            if (pet == null)
                return;

            if (m_temporaryUnsummonedPetNumber == 0 && pet.IsControlled() && !pet.IsTemporarySummoned())
            {
                m_temporaryUnsummonedPetNumber = pet.GetCharmInfo().GetPetNumber();
                m_oldpetspell = pet.m_unitData.CreatedBySpell;
            }

            RemovePet(pet, PetSaveMode.AsCurrent);
        }

        public void ResummonPetTemporaryUnSummonedIfAny()
        {
            if (m_temporaryUnsummonedPetNumber == 0)
                return;

            // not resummon in not appropriate state
            if (IsPetNeedBeTemporaryUnsummoned())
                return;

            if (!GetPetGUID().IsEmpty())
                return;

            Pet NewPet = new(this);
            NewPet.LoadPetFromDB(this, 0, m_temporaryUnsummonedPetNumber, true);

            m_temporaryUnsummonedPetNumber = 0;
        }

        public bool IsPetNeedBeTemporaryUnsummoned()
        {
            return !IsInWorld || !IsAlive() || HasUnitMovementFlag(MovementFlag.Flying) || HasExtraUnitMovementFlag2(MovementFlags3.AdvFlying);
        }

        public void SendRemoveControlBar()
        {
            SendPacket(new PetSpells());
        }

        public void SetBattlePetData(BattlePet pet = null)
        {
            if (pet != null)
            {
                SetSummonedBattlePetGUID(pet.PacketInfo.Guid);
                SetCurrentBattlePetBreedQuality(pet.PacketInfo.Quality);
                SetWildBattlePetLevel(pet.PacketInfo.Level);
            }
            else
            {
                SetSummonedBattlePetGUID(ObjectGuid.Empty);
                SetCurrentBattlePetBreedQuality((byte)BattlePetBreedQuality.Poor);
                SetWildBattlePetLevel(0);
            }
        }

        public void StopCastingCharm()
        {
            Unit charm = GetCharmed();
            if (charm == null)
                return;

            if (charm.IsTypeId(TypeId.Unit))
            {
                if (charm.ToCreature().HasUnitTypeMask(UnitTypeMask.Puppet))
                    ((Puppet)charm).UnSummon();
                else if (charm.IsVehicle())
                {
                    ExitVehicle();

                    // Temporary for issue https://github.com/TrinityCore/TrinityCore/issues/24876
                    if (!GetCharmedGUID().IsEmpty() && !charm.HasAuraTypeWithCaster(AuraType.ControlVehicle, GetGUID()))
                    {
                        Log.outFatal(LogFilter.Player, 
                            $"Player::StopCastingCharm Player '{GetName()}' ({GetGUID()}) " +
                            $"is not able to uncharm vehicle " +
                            $"({GetCharmedGUID()}) because of missing SPELL_AURA_CONTROL_VEHICLE");

                        // attempt to recover from missing HandleAuraControlVehicle unapply handling
                        // THIS IS A HACK, NEED TO FIND HOW IS IT EVEN POSSBLE TO NOT HAVE THE AURA
                        _ExitVehicle();
                    }
                }
            }
            if (!GetCharmedGUID().IsEmpty())
                charm.RemoveCharmAuras();

            if (!GetCharmedGUID().IsEmpty())
            {
                Log.outFatal(LogFilter.Player,
                    $"Player {GetName()} (GUID: {GetGUID()} " +
                    $"is not able to uncharm unit (GUID: {GetCharmedGUID()} " +
                    $"Entry: {charm.GetEntry()}, Type: {charm.GetTypeId()})");

                if (!charm.GetCharmerGUID().IsEmpty())
                {
                    Log.outFatal(LogFilter.Player,
                        $"Player::StopCastingCharm: " +
                        $"Charmed unit has charmer {charm.GetCharmerGUID()}\n" +
                        $"Player debug info: {GetDebugInfo()}\n" +
                        $"Charm debug info: {charm.GetDebugInfo()}");

                    Cypher.Assert(false);
                }

                SetCharm(charm, false);
            }
        }

        public void CharmSpellInitialize()
        {
            Unit charm = GetFirstControlled();
            if (charm == null)
                return;

            CharmInfo charmInfo = charm.GetCharmInfo();
            if (charmInfo == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player:CharmSpellInitialize(): " +
                    $"the player's charm ({charm.GetGUID()}) has no charminfo!");
                return;
            }

            PetSpells petSpells = new();
            petSpells.PetGUID = charm.GetGUID();

            if (charm.IsTypeId(TypeId.Unit))
            {
                petSpells.ReactState = charm.ToCreature().GetReactState();
                petSpells.CommandState = charmInfo.GetCommandState();
            }

            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
                petSpells.ActionButtons[i] = charmInfo.GetActionBarEntry(i).packedData;

            for (byte i = 0; i < SharedConst.MaxSpellCharm; ++i)
            {
                var cspell = charmInfo.GetCharmSpell(i);
                if (cspell.GetAction() != 0)
                    petSpells.Actions.Add(cspell.packedData);
            }

            // Cooldowns
            if (!charm.IsTypeId(TypeId.Player))
                charm.GetSpellHistory().WritePacket(petSpells);

            SendPacket(petSpells);
        }

        public void PossessSpellInitialize()
        {
            Unit charm = GetCharmed();
            if (charm == null)
                return;

            CharmInfo charmInfo = charm.GetCharmInfo();
            if (charmInfo == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player:PossessSpellInitialize(): " +
                    $"charm ({charm.GetGUID()}) has no charminfo!");
                return;
            }

            PetSpells petSpellsPacket = new();
            petSpellsPacket.PetGUID = charm.GetGUID();

            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
                petSpellsPacket.ActionButtons[i] = charmInfo.GetActionBarEntry(i).packedData;

            // Cooldowns
            charm.GetSpellHistory().WritePacket(petSpellsPacket);

            SendPacket(petSpellsPacket);
        }

        public void VehicleSpellInitialize()
        {
            Creature vehicle = GetVehicleCreatureBase();
            if (vehicle == null)
                return;

            PetSpells petSpells = new();
            petSpells.PetGUID = vehicle.GetGUID();
            petSpells.CreatureFamily = 0;                          // Pet Family (0 for all vehicles)
            petSpells.Specialization = 0;
            petSpells.TimeLimit = (Milliseconds)(vehicle.IsSummon() ? vehicle.ToTempSummon().GetTimer().ToMilliseconds() : 0);
            petSpells.ReactState = vehicle.GetReactState();
            petSpells.CommandState = CommandStates.Follow;
            petSpells.Flag = 0x8;

            for (int i = 0; i < SharedConst.MaxSpellControlBar; ++i)
                petSpells.ActionButtons[i] = UnitActionBarEntry.MAKE_UNIT_ACTION_BUTTON(0, (byte)(i + 8));

            for (int i = 0; i < SharedConst.MaxCreatureSpells; ++i)
            {
                int spellId = vehicle.m_spells[i];
                SpellInfo spellInfo = SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());
                if (spellInfo == null)
                    continue;

                if (spellInfo.HasAttribute(SpellAttr5.NotAvailableWhileCharmed))
                    continue;

                if (!ConditionMgr.IsObjectMeetingVehicleSpellConditions(vehicle.GetEntry(), spellId, this, vehicle))
                {
                    Log.outDebug(LogFilter.Condition, 
                        $"VehicleSpellInitialize: conditions not met for Vehicle entry " +
                        $"{vehicle.ToCreature().GetEntry()} spell {spellId}");
                    continue;
                }

                if (spellInfo.IsPassive())
                    vehicle.CastSpell(vehicle, spellInfo.Id, true);

                petSpells.ActionButtons[i] = UnitActionBarEntry.MAKE_UNIT_ACTION_BUTTON(spellId, (byte)(i + 8));
            }

            // Cooldowns
            vehicle.GetSpellHistory().WritePacket(petSpells);

            SendPacket(petSpells);
        }

        //Currency
        void SetCreateCurrency(CurrencyTypes id, int amount)
        {
            SetCreateCurrency((int)id, amount);
        }

        void SetCreateCurrency(int id, int amount)
        {
            if (!_currencyStorage.ContainsKey(id))
            {
                PlayerCurrency playerCurrency = new();
                playerCurrency.state = PlayerCurrencyState.New;
                playerCurrency.Quantity = amount;
                _currencyStorage.Add(id, playerCurrency);
            }
        }

        public void ModifyCurrency(int id, int amount, CurrencyGainSource gainSource = CurrencyGainSource.Cheat, CurrencyDestroyReason destroyReason = CurrencyDestroyReason.Cheat)
        {
            if (amount == 0)
                return;

            CurrencyTypesRecord currency = CliDB.CurrencyTypesStorage.LookupByKey(id);
            Cypher.Assert(currency != null);

            // Check faction
            if ((currency.IsAlliance && GetTeam() != Team.Alliance) ||
                (currency.IsHorde && GetTeam() != Team.Horde))
                return;

            // Check award condition
            if (currency.AwardConditionID != 0)
            {
                PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(currency.AwardConditionID);
                if (playerCondition != null && !ConditionManager.IsPlayerMeetingCondition(this, playerCondition))
                    return;
            }

            bool isGainOnRefund = false;

            if (gainSource == CurrencyGainSource.ItemRefund ||
                gainSource == CurrencyGainSource.PlayerTraitRefund)
                isGainOnRefund = true;

            bool ignoreCaps = isGainOnRefund || gainSource == CurrencyGainSource.QuestRewardIgnoreCaps || gainSource == CurrencyGainSource.WorldQuestRewardIgnoreCaps;

            if (amount > 0 && !isGainOnRefund && gainSource != CurrencyGainSource.Vendor)
            {
                amount = (int)(amount * GetTotalAuraMultiplierByMiscValue(AuraType.ModCurrencyGain, id));
                amount = (int)(amount * GetTotalAuraMultiplierByMiscValue(AuraType.ModCurrencyCategoryGainPct, currency.CategoryID));
            }

            int scaler = currency.Scaler;

            // Currency that is immediately converted into reputation with that faction instead
            FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(currency.FactionID);
            if (factionEntry != null)
            {
                amount /= scaler;
                GetReputationMgr().ModifyReputation(factionEntry, amount, false, true);
                return;
            }

            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
            {
                playerCurrency = new();
                playerCurrency.state = PlayerCurrencyState.New;
                _currencyStorage.Add(id, playerCurrency);
            }

            int weeklyCap = GetCurrencyWeeklyCap(currency);
            if (!ignoreCaps) // Ignore weekly cap for refund
            {
                // Weekly cap
                if (weeklyCap != 0 && amount > 0 && (playerCurrency.WeeklyQuantity + amount) > weeklyCap)
                    amount = weeklyCap - playerCurrency.WeeklyQuantity;

                // Max cap
                int maxCap = GetCurrencyMaxQuantity(currency, false, gainSource == CurrencyGainSource.UpdatingVersion);
                if (maxCap != 0 && amount > 0 && (playerCurrency.Quantity + amount) > maxCap)
                    amount = maxCap - playerCurrency.Quantity;
            }

            // Underflow protection
            if (amount < 0 && Math.Abs(amount) > playerCurrency.Quantity)
                amount = -playerCurrency.Quantity;

            if (amount == 0)
                return;

            if (playerCurrency.state != PlayerCurrencyState.New)
                playerCurrency.state = PlayerCurrencyState.Changed;

            playerCurrency.Quantity += amount;

            if (amount > 0 && !ignoreCaps) // Ignore total values update for refund
            {
                if (weeklyCap != 0)
                    playerCurrency.WeeklyQuantity += amount;

                if (currency.IsTrackingQuantity)
                    playerCurrency.TrackedQuantity += amount;

                if (currency.HasTotalEarned)
                    playerCurrency.EarnedQuantity += amount;

                if (!isGainOnRefund)
                    UpdateCriteria(CriteriaType.CurrencyGained, id, amount);
            }

            CurrencyChanged(id, amount);

            SetCurrency packet = new();
            packet.Type = currency.Id;
            packet.Quantity = playerCurrency.Quantity;
            packet.Flags = CurrencyGainFlags.None; // TODO: Check when flags are applied

            if ((playerCurrency.WeeklyQuantity / currency.Scaler) > 0)
                packet.WeeklyQuantity = playerCurrency.WeeklyQuantity;

            if (currency.HasMaxQuantity(false, gainSource == CurrencyGainSource.UpdatingVersion))
                packet.MaxQuantity = GetCurrencyMaxQuantity(currency);

            if (currency.HasTotalEarned)
                packet.TotalEarned = playerCurrency.EarnedQuantity;

            packet.SuppressChatLog = currency.IsSuppressingChatLog(gainSource == CurrencyGainSource.UpdatingVersion);
            packet.QuantityChange = amount;

            if (amount > 0)
                packet.QuantityGainSource = gainSource;
            else
                packet.QuantityLostSource = destroyReason;

            // TODO: FirstCraftOperationID, LastSpendTime & Toasts
            SendPacket(packet);
        }

        public void AddCurrency(int id, int amount, CurrencyGainSource gainSource = CurrencyGainSource.Cheat)
        {
            ModifyCurrency(id, amount, gainSource);
        }

        public void RemoveCurrency(int id, int amount, CurrencyDestroyReason destroyReason = CurrencyDestroyReason.Cheat)
        {
            ModifyCurrency(id, -amount, default, destroyReason);
        }

        public void IncreaseCurrencyCap(int id, int amount)
        {
            if (amount == 0)
                return;

            CurrencyTypesRecord currency = CliDB.CurrencyTypesStorage.LookupByKey(id);
            Cypher.Assert(currency != null);

            // Check faction
            if ((currency.IsAlliance && GetTeam() != Team.Alliance) ||
                (currency.IsHorde && GetTeam() != Team.Horde))
                return;

            // Check dynamic maximum flag
            if (!currency.HasFlag(CurrencyTypesFlags.DynamicMaximum))
                return;

            // Ancient mana maximum cap
            if (id == (int)CurrencyTypes.AncientMana)
            {
                int maxQuantity = GetCurrencyMaxQuantity(currency);

                if ((maxQuantity + amount) > PlayerConst.CurrencyMaxCapAncientMana)
                    amount = PlayerConst.CurrencyMaxCapAncientMana - maxQuantity;
            }

            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
            {
                playerCurrency = new();
                playerCurrency.state = PlayerCurrencyState.New;
                playerCurrency.IncreasedCapQuantity = amount;
                _currencyStorage[id] = playerCurrency;
            }
            else
            {
                playerCurrency.IncreasedCapQuantity += amount;
            }

            if (playerCurrency.state != PlayerCurrencyState.New)
                playerCurrency.state = PlayerCurrencyState.Changed;

            SetCurrency packet = new();
            packet.Type = currency.Id;
            packet.Quantity = playerCurrency.Quantity;
            packet.Flags = CurrencyGainFlags.None;

            if ((playerCurrency.WeeklyQuantity / currency.Scaler) > 0)
                packet.WeeklyQuantity = playerCurrency.WeeklyQuantity;

            if (currency.IsTrackingQuantity)
                packet.TrackedQuantity = playerCurrency.TrackedQuantity;

            packet.MaxQuantity = GetCurrencyMaxQuantity(currency);
            packet.SuppressChatLog = currency.IsSuppressingChatLog();

            SendPacket(packet);
        }

        public int GetCurrencyQuantity(int id)
        {
            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
                return 0;

            return playerCurrency.Quantity;
        }

        public int GetCurrencyWeeklyQuantity(int id)
        {
            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
                return 0;

            return playerCurrency.WeeklyQuantity;
        }

        public int GetCurrencyTrackedQuantity(int id)
        {
            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
                return 0;

            return playerCurrency.TrackedQuantity;
        }

        int GetCurrencyIncreasedCapQuantity(int id)
        {
            var playerCurrency = _currencyStorage.LookupByKey(id);
            if (playerCurrency == null)
                return 0;

            return playerCurrency.IncreasedCapQuantity;
        }

        public int GetCurrencyMaxQuantity(CurrencyTypesRecord currency, bool onLoad = false, bool onUpdateVersion = false)
        {
            if (!currency.HasMaxQuantity(onLoad, onUpdateVersion))
                return 0;

            int maxQuantity = currency.MaxQty;
            int increasedCap = 0;

            if (currency.HasFlag(CurrencyTypesFlags.DynamicMaximum))
                increasedCap = GetCurrencyIncreasedCapQuantity(currency.Id);

            return maxQuantity + increasedCap;
        }

        int GetCurrencyWeeklyCap(int id)
        {
            CurrencyTypesRecord currency = CliDB.CurrencyTypesStorage.LookupByKey(id);
            if (currency == null)
                return 0;

            return GetCurrencyWeeklyCap(currency);
        }

        int GetCurrencyWeeklyCap(CurrencyTypesRecord currency)
        {
            // TODO: CurrencyTypeFlags::ComputedWeeklyMaximum
            return currency.MaxEarnablePerWeek;
        }

        public bool HasCurrency(int id, int amount)
        {
            var playerCurrency = _currencyStorage.LookupByKey(id);
            return playerCurrency != null && playerCurrency.Quantity >= amount;
        }

        //Action Buttons - CUF Profile
        public void SaveCUFProfile(byte id, CUFProfile profile) { _CUFProfiles[id] = profile; }
        public CUFProfile GetCUFProfile(byte id) { return _CUFProfiles[id]; }

        public byte GetCUFProfilesCount()
        {
            return (byte)_CUFProfiles.Count(p => p != null);
        }

        bool IsActionButtonDataValid(byte button, int action, ActionButtonType type)
        {
            if (button >= PlayerConst.MaxActionButtons)
            {
                Log.outError(LogFilter.Player, 
                    $"Player::IsActionButtonDataValid: Action {action} not added into button {button} " +
                    $"for player {GetName()} ({GetGUID()}): " +
                    $"button must be < {PlayerConst.MaxActionButtons}");
                return false;
            }

            if (action >= PlayerConst.MaxActionButtonActionValue)
            {
                Log.outError(LogFilter.Player, 
                    $"Player::IsActionButtonDataValid: Action {action} not added into button {button} " +
                    $"for player {GetName()} ({GetGUID()}): " +
                    $"action must be < {PlayerConst.MaxActionButtonActionValue}");
                return false;
            }

            switch (type)
            {
                case ActionButtonType.Spell:
                    if (!SpellMgr.HasSpellInfo(action, Difficulty.None))
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::IsActionButtonDataValid: " +
                            $"Spell action {action} not added into button {button} " +
                            $"for player {GetName()} ({GetGUID()}): " +
                            $"spell not exist");
                        return false;
                    }
                    break;
                case ActionButtonType.Item:
                    if (ObjectMgr.GetItemTemplate(action) == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::IsActionButtonDataValid: " +
                            $"Item action {action} not added into button {button} " +
                            $"for player {GetName()} ({GetGUID()}): " +
                            $"item not exist");
                        return false;
                    }
                    break;
                case ActionButtonType.Companion:
                {
                    if (GetSession().GetBattlePetMgr().GetPet(ObjectGuid.Create(HighGuid.BattlePet, action)) == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::IsActionButtonDataValid: " +
                            $"Companion action {action} not added into button {button} " +
                            $"for player {GetName()} ({GetGUID()}): " +
                            $"companion does not exist");
                        return false;
                    }
                    break;
                }
                case ActionButtonType.Mount:
                    var mount = CliDB.MountStorage.LookupByKey(action);
                    if (mount == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::IsActionButtonDataValid: " +
                            $"Mount action {action} not added into button {button} " +
                            $"for player {GetName()} ({GetGUID()}): " +
                            $"mount does not exist");
                        return false;
                    }

                    if (!HasSpell(mount.SourceSpellID))
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::IsActionButtonDataValid: " +
                            $"Mount action {action} not added into button {button} " +
                            $"for player {GetName()} ({GetGUID()}): " +
                            $"Player does not know this mount");
                        return false;
                    }
                    break;
                case ActionButtonType.C:
                case ActionButtonType.CMacro:
                case ActionButtonType.Macro:
                case ActionButtonType.Eqset:
                    break;
                default:
                    Log.outError(LogFilter.Player, $"Unknown action Type {type}");
                    return false;                                          // other cases not checked at this moment
            }

            return true;
        }

        public void SetMultiActionBars(byte mask) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.MultiActionBars), mask); }

        public ActionButton AddActionButton(byte button, int action, ActionButtonType type)
        {
            if (!IsActionButtonDataValid(button, action, type))
                return null;

            // it create new button (NEW state) if need or return existed
            if (!m_actionButtons.ContainsKey(button))
                m_actionButtons[button] = new ActionButton();

            var ab = m_actionButtons[button];

            // set data and update to CHANGED if not NEW
            ab.SetActionAndType(action, type);

            Log.outDebug(LogFilter.Player, 
                $"Player::AddActionButton: " +
                $"Player '{GetName()}' ({GetGUID()}) " +
                $"added action '{action}' (Type {type}) to button '{button}'");

            return ab;
        }

        public void RemoveActionButton(byte _button)
        {
            var button = m_actionButtons.LookupByKey(_button);
            if (button == null || button.uState == ActionButtonUpdateState.Deleted)
                return;

            if (button.uState == ActionButtonUpdateState.New)
                m_actionButtons.Remove(_button);                   // new and not saved
            else
                button.uState = ActionButtonUpdateState.Deleted;    // saved, will deleted at next save

            Log.outDebug(LogFilter.Player, 
                $"Action Button '{button}' Removed from Player '{GetGUID()}'");
        }

        public ActionButton GetActionButton(byte _button)
        {
            var button = m_actionButtons.LookupByKey(_button);
            if (button == null || button.uState == ActionButtonUpdateState.Deleted)
                return null;

            return button;
        }

        void SendInitialActionButtons() { SendActionButtons(0); }

        void SendActionButtons(ActionsButtonsUpdateReason state)
        {
            UpdateActionButtons packet = new();

            foreach (var pair in m_actionButtons)
            {
                if (pair.Value.uState != ActionButtonUpdateState.Deleted && pair.Key < packet.ActionButtons.Length)
                    packet.ActionButtons[pair.Key] = pair.Value.packedData;
            }

            packet.Reason = state;
            SendPacket(packet);
        }

        //Reputation
        public int CalculateReputationGain(ReputationSource source, int creatureOrQuestLevel, int rep, int faction, bool noQuestBonus = false)
        {
            float percent = 100.0f;

            float repMod = noQuestBonus ? 0.0f : GetTotalAuraModifier(AuraType.ModReputationGain);

            // faction specific auras only seem to apply to kills
            if (source == ReputationSource.Kill)
                repMod += GetTotalAuraModifierByMiscValue(AuraType.ModFactionReputationGain, faction);

            percent += rep > 0 ? repMod : -repMod;

            float rate;
            switch (source)
            {
                case ReputationSource.Kill:
                    rate = WorldConfig.Values[WorldCfg.RateReputationLowLevelKill].Float;
                    break;
                case ReputationSource.Quest:
                case ReputationSource.DailyQuest:
                case ReputationSource.WeeklyQuest:
                case ReputationSource.MonthlyQuest:
                case ReputationSource.RepeatableQuest:
                    rate = WorldConfig.Values[WorldCfg.RateReputationLowLevelQuest].Float;
                    break;
                case ReputationSource.Spell:
                default:
                    rate = 1.0f;
                    break;
            }

            if (rate != 1.0f && creatureOrQuestLevel < Formulas.GetGrayLevel(GetLevel()))
                percent *= rate;

            if (percent <= 0.0f)
                return 0;

            // Multiply result with the faction specific rate
            RepRewardRate repData = ObjectMgr.GetRepRewardRate(faction);
            if (repData != null)
            {
                float repRate = 0.0f;
                switch (source)
                {
                    case ReputationSource.Kill:
                        repRate = repData.creatureRate;
                        break;
                    case ReputationSource.Quest:
                        repRate = repData.questRate;
                        break;
                    case ReputationSource.DailyQuest:
                        repRate = repData.questDailyRate;
                        break;
                    case ReputationSource.WeeklyQuest:
                        repRate = repData.questWeeklyRate;
                        break;
                    case ReputationSource.MonthlyQuest:
                        repRate = repData.questMonthlyRate;
                        break;
                    case ReputationSource.RepeatableQuest:
                        repRate = repData.questRepeatableRate;
                        break;
                    case ReputationSource.Spell:
                        repRate = repData.spellRate;
                        break;
                }

                // for custom, a rate of 0.0 will totally disable reputation gain for this faction/Type
                if (repRate <= 0.0f)
                    return 0;

                percent *= repRate;
            }

            if (source != ReputationSource.Spell && GetsRecruitAFriendBonus(false))
                percent *= 1.0f + WorldConfig.Values[WorldCfg.RateReputationRecruitAFriendBonus].Float;

            return MathFunctions.CalculatePct(rep, percent);
        }

        // Calculates how many reputation points player gains in victim's enemy factions
        public void RewardReputation(Unit victim, float rate)
        {
            if (victim == null || victim.IsTypeId(TypeId.Player))
                return;

            if (victim.ToCreature().IsReputationGainDisabled())
                return;

            ReputationOnKillEntry Rep = ObjectMgr.GetReputationOnKilEntry(victim.ToCreature().GetCreatureTemplate().Entry);
            if (Rep == null)
                return;

            int ChampioningFaction = 0;

            if (GetChampioningFaction() != 0)
            {
                // support for: Championing - http://www.wowwiki.com/Championing
                Map map = GetMap();
                if (map.IsNonRaidDungeon())
                {
                    LFGDungeonsRecord dungeon = DB2Mgr.GetLfgDungeon(map.GetId(), map.GetDifficultyID());
                    if (dungeon != null)
                    {
                        if (dungeon.TargetLevel == ObjectMgr.GetMaxLevelForExpansion(Expansion.WrathOfTheLichKing))
                                ChampioningFaction = GetChampioningFaction();
                    }
                }
            }

            Team team = GetTeam();

            if (Rep.RepFaction1 != 0 && (!Rep.TeamDependent || team == Team.Alliance))
            {
                int donerep1 = CalculateReputationGain(ReputationSource.Kill, victim.GetLevelForTarget(this), Rep.RepValue1, (ChampioningFaction != 0 ? ChampioningFaction : Rep.RepFaction1));
                donerep1 = (int)(donerep1 * rate);

                FactionRecord factionEntry1 = CliDB.FactionStorage.LookupByKey(ChampioningFaction != 0 ? ChampioningFaction : Rep.RepFaction1);
                ReputationRank current_reputation_rank1 = GetReputationMgr().GetRank(factionEntry1);
                if (factionEntry1 != null)
                    GetReputationMgr().ModifyReputation(factionEntry1, donerep1, (uint)current_reputation_rank1 > Rep.ReputationMaxCap1);
            }

            if (Rep.RepFaction2 != 0 && (!Rep.TeamDependent || team == Team.Horde))
            {
                int donerep2 = CalculateReputationGain(ReputationSource.Kill, victim.GetLevelForTarget(this), Rep.RepValue2, ChampioningFaction != 0 ? ChampioningFaction : Rep.RepFaction2);
                donerep2 = (int)(donerep2 * rate);

                FactionRecord factionEntry2 = CliDB.FactionStorage.LookupByKey(ChampioningFaction != 0 ? ChampioningFaction : Rep.RepFaction2);
                ReputationRank current_reputation_rank2 = GetReputationMgr().GetRank(factionEntry2);
                if (factionEntry2 != null)
                    GetReputationMgr().ModifyReputation(factionEntry2, donerep2, (uint)current_reputation_rank2 > Rep.ReputationMaxCap2);
            }
        }

        // Calculate how many reputation points player gain with the quest
        void RewardReputation(Quest quest)
        {
            for (byte i = 0; i < SharedConst.QuestRewardReputationsCount; ++i)
            {
                if (quest.RewardFactionId[i] == 0)
                    continue;

                FactionRecord factionEntry = CliDB.FactionStorage.LookupByKey(quest.RewardFactionId[i]);
                if (factionEntry == null)
                    continue;

                int rep = 0;
                bool noQuestBonus = false;

                if (quest.RewardFactionOverride[i] != 0)
                {
                    rep = quest.RewardFactionOverride[i] / 100;
                    noQuestBonus = true;
                }
                else
                {
                    var row = (quest.RewardFactionValue[i] < 0 ? 1 : 0) + 1;
                    QuestFactionRewardRecord questFactionRewEntry = CliDB.QuestFactionRewardStorage.LookupByKey(row);
                    if (questFactionRewEntry != null)
                    {
                        uint field = (uint)Math.Abs(quest.RewardFactionValue[i]);
                        rep = questFactionRewEntry.Difficulty[field];
                    }
                }

                if (rep == 0)
                    continue;

                if (quest.RewardFactionCapIn[i] != 0 && rep > 0 && (int)GetReputationMgr().GetRank(factionEntry) >= quest.RewardFactionCapIn[i])
                    continue;

                if (quest.IsDaily())
                    rep = CalculateReputationGain(ReputationSource.DailyQuest, GetQuestLevel(quest), rep, quest.RewardFactionId[i], noQuestBonus);
                else if (quest.IsWeekly())
                    rep = CalculateReputationGain(ReputationSource.WeeklyQuest, GetQuestLevel(quest), rep, quest.RewardFactionId[i], noQuestBonus);
                else if (quest.IsMonthly())
                    rep = CalculateReputationGain(ReputationSource.MonthlyQuest, GetQuestLevel(quest), rep, quest.RewardFactionId[i], noQuestBonus);
                else if (quest.IsRepeatable())
                    rep = CalculateReputationGain(ReputationSource.RepeatableQuest, GetQuestLevel(quest), rep, quest.RewardFactionId[i], noQuestBonus);
                else
                    rep = CalculateReputationGain(ReputationSource.Quest, GetQuestLevel(quest), rep, quest.RewardFactionId[i], noQuestBonus);

                bool noSpillover = Convert.ToBoolean(quest.RewardReputationMask & (1 << i));
                GetReputationMgr().ModifyReputation(factionEntry, rep, false, noSpillover);
            }
        }

        //Movement
        bool IsCanDelayTeleport() { return m_bCanDelayTeleport; }
        void SetCanDelayTeleport(bool setting) { m_bCanDelayTeleport = setting; }
        bool IsHasDelayedTeleport() { return m_bHasDelayedTeleport; }
        void SetDelayedTeleportFlag(bool setting) { m_bHasDelayedTeleport = setting; }

        public bool TeleportTo(WorldLocation loc, TeleportToOptions options = 0, int? instanceId = null)
        {
            return TeleportTo(loc.GetMapId(), loc.posX, loc.posY, loc.posZ, loc.Orientation, options, instanceId);
        }

        public bool TeleportTo(int mapid, float x, float y, float z, float orientation, TeleportToOptions options = 0, int? instanceId = null)
        {
            if (!GridDefines.IsValidMapCoord(mapid, x, y, z, orientation))
            {
                Log.outError(LogFilter.Maps, 
                    $"TeleportTo: invalid map ({mapid}) or invalid coordinates (X: {x}, Y: {y}, Z: {z}, O: {orientation}) " +
                    $"given when teleporting player (GUID: {GetGUID()}, name: {GetName()}, map: {GetMapId()}, {GetPosition()}).");
                return false;
            }

            if (!GetSession().HasPermission(RBACPermissions.SkipCheckDisableMap) && DisableMgr.IsDisabledFor(DisableType.Map, mapid, this))
            {
                Log.outError(LogFilter.Maps, 
                    $"Player (GUID: {GetGUID()}, name: {GetName()}) tried to enter a forbidden map {mapid}");

                SendTransferAborted(mapid, TransferAbortReason.MapNotAllowed);
                return false;
            }

            // preparing unsummon pet if lost (we must get pet before teleportation or will not find it later)
            Pet pet = GetPet();

            MapRecord mEntry = CliDB.MapStorage.LookupByKey(mapid);

            // don't let enter Battlegrounds without assigned Battlegroundid (for example through areatrigger)...
            // don't let gm level > 1 either
            if (!InBattleground() && mEntry.IsBattlegroundOrArena)
                return false;

            // client without expansion support
            if (GetSession().GetExpansion() < mEntry.Expansion)
            {
                Log.outDebug(LogFilter.Maps, 
                    $"Player {GetName()} using client without required expansion " +
                    $"tried teleport to non accessible map {mapid}");

                ITransport _transport = GetTransport();
                if (_transport != null)
                {
                    _transport.RemovePassenger(this);
                    RepopAtGraveyard();                             // teleport to near graveyard if on transport, looks blizz like :)
                }

                SendTransferAborted(mapid, TransferAbortReason.InsufExpanLvl, (byte)mEntry.Expansion);
                return false;                                       // normal client can't teleport to this map...
            }
            else
                Log.outDebug(LogFilter.Maps, $"Player {GetName()} is being teleported to map {mapid}");

            if (m_vehicle != null)
                ExitVehicle();

            // reset movement flags at teleport, because player will continue move with these flags after teleport
            SetUnitMovementFlags(GetUnitMovementFlags() & MovementFlag.MaskHasPlayerStatusOpcode);
            m_movementInfo.ResetJump();
            DisableSpline();
            GetMotionMaster().Remove(MovementGeneratorType.Effect);

            ITransport transport = GetTransport();
            if (transport != null)
            {
                if (!options.HasAnyFlag(TeleportToOptions.NotLeaveTransport))
                    transport.RemovePassenger(this);
            }

            // The player was ported to another map and loses the duel immediately.
            // We have to perform this check before the teleport, otherwise the
            // ObjectAccessor won't find the flag.
            if (duel != null && GetMapId() != mapid && GetMap().GetGameObject(m_playerData.DuelArbiter) != null)
                DuelComplete(DuelCompleteType.Fled);

            if (GetMapId() == mapid && (!instanceId.HasValue || GetInstanceId() == instanceId))
            {
                //lets reset far teleport flag if it wasn't reset during chained teleports
                SetSemaphoreTeleportFar(false);
                //setup delayed teleport flag
                SetDelayedTeleportFlag(IsCanDelayTeleport());
                //if teleport spell is casted in Unit.Update() func
                //then we need to delay it until update process will be finished
                if (IsHasDelayedTeleport())
                {
                    SetSemaphoreTeleportNear(true);
                    //lets save teleport destination for player
                    teleportDest = new WorldLocation(mapid, x, y, z, orientation);
                    m_teleport_instanceId = null;
                    m_teleport_options = options;
                    return true;
                }

                if (!options.HasAnyFlag(TeleportToOptions.NotUnSummonPet))
                {
                    //same map, only remove pet if out of range for new position
                    if (pet != null && !pet.IsWithinDist3d(x, y, z, GetMap().GetVisibilityRange()))
                        UnsummonPetTemporaryIfAny();
                }

                if (!IsAlive() && options.HasAnyFlag(TeleportToOptions.ReviveAtTeleport))
                    ResurrectPlayer(0.5f);

                if (!options.HasAnyFlag(TeleportToOptions.NotLeaveCombat))
                    CombatStop();

                // this will be used instead of the current location in SaveToDB
                teleportDest = new WorldLocation(mapid, x, y, z, orientation);
                m_teleport_instanceId = null;
                m_teleport_options = options;
                SetFallInformation(0, GetPositionZ());

                // code for finish transfer called in WorldSession.HandleMovementOpcodes()
                // at client packet CMSG_MOVE_TELEPORT_ACK
                SetSemaphoreTeleportNear(true);
                // near teleport, triggering send CMSG_MOVE_TELEPORT_ACK from client at landing
                if (!GetSession().PlayerLogout())
                    SendTeleportPacket(teleportDest);
            }
            else
            {
                if (GetClass() == Class.DeathKnight && GetMapId() == 609 && !IsGameMaster() && !HasSpell(50977))
                {
                    SendTransferAborted(mapid, TransferAbortReason.UniqueMessage, 1);
                    return false;
                }

                // far teleport to another map
                Map oldmap = IsInWorld ? GetMap() : null;
                // check if we can enter before stopping combat / removing pet / totems / interrupting spells

                // Check enter rights before map getting to avoid creating instance copy for player
                // this check not dependent from map instance copy and same for all instance copies of selected map
                TransferAbortParams abortParams = Map.PlayerCannotEnter(mapid, this);
                if (abortParams != null)
                {
                    SendTransferAborted(mapid, abortParams.Reason, abortParams.Arg, abortParams.MapDifficultyXConditionId);
                    return false;
                }

                // Seamless teleport can happen only if cosmetic maps match
                if (oldmap == null || (oldmap.GetEntry().CosmeticParentMapID != mapid && GetMapId() != mEntry.CosmeticParentMapID &&
                    !((oldmap.GetEntry().CosmeticParentMapID != -1) ^ (oldmap.GetEntry().CosmeticParentMapID != mEntry.CosmeticParentMapID))))
                    options &= ~TeleportToOptions.Seamless;

                //lets reset near teleport flag if it wasn't reset during chained teleports
                SetSemaphoreTeleportNear(false);
                //setup delayed teleport flag
                SetDelayedTeleportFlag(IsCanDelayTeleport());
                //if teleport spell is cast in Unit::Update() func
                //then we need to delay it until update process will be finished
                if (IsHasDelayedTeleport())
                {
                    SetSemaphoreTeleportFar(true);
                    //lets save teleport destination for player
                    teleportDest = new(mapid, x, y, z, orientation);
                    m_teleport_instanceId = instanceId;
                    m_teleport_options = options;
                    return true;
                }

                SetSelection(ObjectGuid.Empty);

                CombatStop();

                ResetContestedPvP();

                // remove player from Battlegroundon far teleport (when changing maps)
                Battleground bg = GetBattleground();
                if (bg != null)
                {
                    // Note: at Battlegroundjoin Battlegroundid set before teleport
                    // and we already will found "current" Battleground
                    // just need check that this is targeted map or leave
                    if (bg.GetMapId() != mapid)
                        LeaveBattleground(false);                   // don't teleport to entry point
                }

                // remove arena spell coldowns/buffs now to also remove pet's cooldowns
                // before it's temporarily unsummoned
                if (mEntry.IsBattleArena && !IsGameMaster())
                {
                    RemoveArenaSpellCooldowns(true);
                    RemoveArenaAuras();
                    if (pet != null)
                        pet.RemoveArenaAuras();
                }

                // remove pet on map change
                if (pet != null)
                    UnsummonPetTemporaryIfAny();

                // remove all dyn objects
                RemoveAllDynObjects();

                // remove all areatriggers entities
                RemoveAllAreaTriggers();

                // stop spellcasting
                // not attempt interrupt teleportation spell at caster teleport
                if (!options.HasAnyFlag(TeleportToOptions.Spell))
                    if (IsNonMeleeSpellCast(true))
                        InterruptNonMeleeSpells(true);

                //remove auras before removing from map...
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Moving | SpellAuraInterruptFlags.Turning);

                if (!GetSession().PlayerLogout() && !options.HasAnyFlag(TeleportToOptions.Seamless))
                {
                    // send transfer packets
                    TransferPending transferPending = new();
                    transferPending.MapID = mapid;
                    transferPending.OldMapPosition = GetPosition();

                    Transport transport1 = (Transport)GetTransport();
                    if (transport1 != null)
                    {
                        TransferPending.ShipTransferPending shipTransferPending = new();
                        shipTransferPending.Id = transport1.GetEntry();
                        shipTransferPending.OriginMapID = GetMapId();
                        transferPending.Ship = shipTransferPending;
                    }

                    SendPacket(transferPending);

                    RemovePlayerLocalFlag(PlayerLocalFlags.OverrideTransportServerTime);
                    SetTransportServerTime(default);
                }

                // remove from old map now
                if (oldmap != null)
                    oldmap.RemovePlayerFromMap(this, false);

                teleportDest = new WorldLocation(mapid, x, y, z, orientation);
                m_teleport_instanceId = instanceId;
                m_teleport_options = options;
                SetFallInformation(0, GetPositionZ());
                // if the player is saved before worldportack (at logout for example)
                // this will be used instead of the current location in SaveToDB

                if (!GetSession().PlayerLogout())
                {
                    SuspendToken suspendToken = new();
                    suspendToken.SequenceIndex = m_movementCounter; // not incrementing
                    suspendToken.Reason = options.HasAnyFlag(TeleportToOptions.Seamless) ? 2 : 1;
                    SendPacket(suspendToken);
                }

                // move packet sent by client always after far teleport
                // code for finish transfer to new map
                // called in WorldSession.HandleMoveWorldportAckOpcode at client packet
                SetSemaphoreTeleportFar(true);

            }
            return true;
        }

        public bool TeleportToBGEntryPoint()
        {
            if (m_bgData.joinPos.GetMapId() == -1)
                return false;

            ScheduleDelayedOperation(PlayerDelayedOperations.BGMountRestore);
            ScheduleDelayedOperation(PlayerDelayedOperations.BGTaxiRestore);
            ScheduleDelayedOperation(PlayerDelayedOperations.BGGroupRestore);
            return TeleportTo(m_bgData.joinPos);
        }

        public int GetStartLevel(Race race, Class playerClass, int? characterTemplateId = null)
        {
            int startLevel = WorldConfig.Values[WorldCfg.StartPlayerLevel].Int32;
            if (CliDB.ChrRacesStorage.LookupByKey((int)race).HasFlag(ChrRacesFlag.IsAlliedRace))
                startLevel = WorldConfig.Values[WorldCfg.StartAlliedRaceLevel].Int32;

            if (playerClass == Class.DeathKnight)
            {
                if (race == Race.PandarenAlliance || race == Race.PandarenHorde)
                    startLevel = Math.Max(WorldConfig.Values[WorldCfg.StartAlliedRaceLevel].Int32, startLevel);
                else
                startLevel = Math.Max(WorldConfig.Values[WorldCfg.StartDeathKnightPlayerLevel].Int32, startLevel);
            }
            else if (playerClass == Class.DemonHunter)
                startLevel = Math.Max(WorldConfig.Values[WorldCfg.StartDemonHunterPlayerLevel].Int32, startLevel);
            else if (playerClass == Class.Evoker)
                startLevel = Math.Max(WorldConfig.Values[WorldCfg.StartEvokerPlayerLevel].Int32, startLevel);

            if (characterTemplateId.HasValue)
            {
                if (GetSession().HasPermission(RBACPermissions.UseCharacterTemplates))
                {
                    CharacterTemplate charTemplate = Global.CharacterTemplateDataStorage.GetCharacterTemplate(characterTemplateId.Value);
                    if (charTemplate != null)
                        startLevel = Math.Max(charTemplate.Level, startLevel);
                }
                else
                {
                    Log.outWarn(LogFilter.Cheat,
                        $"Account: {GetSession().GetAccountId()} (IP: {GetSession().GetRemoteAddress()}) " +
                        $"tried to use a character template without given permission. Possible cheating attempt.");
                }
            }

            if (GetSession().HasPermission(RBACPermissions.UseStartGmLevel))
                startLevel = Math.Max(WorldConfig.Values[WorldCfg.StartGmLevel].Int32, startLevel);

            return startLevel;
        }

        public void ValidateMovementInfo(MovementInfo mi)
        {
            var RemoveViolatingFlags = new Action<bool, MovementFlag>((check, maskToRemove) =>
            {
                if (check)
                {
                    Log.outDebug(LogFilter.Unit, 
                        $"Player.ValidateMovementInfo: " +
                        $"Violation of MovementFlags found ({check}). " +
                        $"MovementFlags: {mi.GetMovementFlags()}, " +
                        $"MovementFlags2: {mi.GetMovementFlags2()} for player {GetGUID()}. " +
                        $"Mask {maskToRemove} will be removed.");

                    mi.RemoveMovementFlag(maskToRemove);
                }
            });

            if (m_unitMovedByMe.GetVehicleBase() == null || !m_unitMovedByMe.GetVehicle().GetVehicleInfo().HasFlag(VehicleFlags.FixedPosition))
                RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Root), MovementFlag.Root);

            /*! This must be a packet spoofing attempt. MOVEMENTFLAG_ROOT sent from the client is not valid
                in conjunction with any of the moving movement flags such as MOVEMENTFLAG_FORWARD.
                It will freeze clients that receive this player's movement info.
            */
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Root) && mi.HasMovementFlag(MovementFlag.MaskMoving), MovementFlag.MaskMoving);

            //! Cannot hover without SPELL_AURA_HOVER
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Hover) && !m_unitMovedByMe.HasAuraType(AuraType.Hover),
                MovementFlag.Hover);

            //! Cannot ascend and descend at the same time
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Ascending) && mi.HasMovementFlag(MovementFlag.Descending),
                MovementFlag.Ascending | MovementFlag.Descending);

            //! Cannot move left and right at the same time
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Left) && mi.HasMovementFlag(MovementFlag.Right),
                MovementFlag.Left | MovementFlag.Right);

            //! Cannot strafe left and right at the same time
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.StrafeLeft) && mi.HasMovementFlag(MovementFlag.StrafeRight),
                MovementFlag.StrafeLeft | MovementFlag.StrafeRight);

            //! Cannot pitch up and down at the same time
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.PitchUp) && mi.HasMovementFlag(MovementFlag.PitchDown),
                MovementFlag.PitchUp | MovementFlag.PitchDown);

            //! Cannot move forwards and backwards at the same time
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Forward) && mi.HasMovementFlag(MovementFlag.Backward),
                MovementFlag.Forward | MovementFlag.Backward);

            //! Cannot walk on water without SPELL_AURA_WATER_WALK except for ghosts
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.WaterWalk) &&
                !m_unitMovedByMe.HasAuraType(AuraType.WaterWalk) && !m_unitMovedByMe.HasAuraType(AuraType.Ghost), MovementFlag.WaterWalk);

            //! Cannot feather fall without SPELL_AURA_FEATHER_FALL
            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.FallingSlow) && !m_unitMovedByMe.HasAuraType(AuraType.FeatherFall),
                MovementFlag.FallingSlow);

            /*! Cannot fly if no fly auras present. Exception is being a GM.
                Note that we check for account level instead of Player.IsGameMaster() because in some
                situations it may be feasable to use .gm fly on as a GM without having .gm on,
                e.g. aerial combat.
            */

            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.Flying | MovementFlag.CanFly) && GetSession().GetSecurity() == AccountTypes.Player &&
                !m_unitMovedByMe.HasAuraType(AuraType.Fly) &&
                !m_unitMovedByMe.HasAuraType(AuraType.ModIncreaseMountedFlightSpeed),
                MovementFlag.Flying | MovementFlag.CanFly);

            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.DisableGravity | MovementFlag.CanFly) && mi.HasMovementFlag(MovementFlag.Falling),
                MovementFlag.Falling);

            RemoveViolatingFlags(mi.HasMovementFlag(MovementFlag.SplineElevation) && MathFunctions.fuzzyEq(mi.stepUpStartElevation, 0.0f), MovementFlag.SplineElevation);

            // Client first checks if spline elevation != 0, then verifies flag presence
            if (MathFunctions.fuzzyNe(mi.stepUpStartElevation, 0.0f))
                mi.AddMovementFlag(MovementFlag.SplineElevation);
        }

        public void HandleFall(MovementInfo movementInfo)
        {
            // calculate total z distance of the fall
            float z_diff = m_lastFallZ - movementInfo.Pos.posZ;
            Log.outDebug(LogFilter.Server, $"zDiff = {z_diff}");

            //Players with low fall distance, Feather Fall or physical immunity (charges used) are ignored
            // 14.57 can be calculated by resolving damageperc formula below to 0
            if (z_diff >= 14.57f && !IsDead() && !IsGameMaster() &&
                !HasAuraType(AuraType.Hover) && !HasAuraType(AuraType.FeatherFall) &&
                !HasAuraType(AuraType.Fly) && !IsImmunedToDamage(SpellSchoolMask.Normal))
            {
                //Safe fall, fall height reduction
                int safe_fall = GetTotalAuraModifier(AuraType.SafeFall);

                float damageperc = 0.018f * (z_diff - safe_fall) - 0.2426f;

                if (damageperc > 0)
                {
                    int damage = (int)(damageperc * GetMaxHealth() * WorldConfig.Values[WorldCfg.RateDamageFall].Float);

                    float height = movementInfo.Pos.posZ;
                    UpdateGroundPositionZ(movementInfo.Pos.posX, movementInfo.Pos.posY, ref height);

                    damage = (int)(damage * GetTotalAuraMultiplier(AuraType.ModifyFallDamagePct));

                    if (damage > 0)
                    {
                        //Prevent fall damage from being more than the player maximum health
                        if (damage > GetMaxHealth())
                            damage = (int)GetMaxHealth();

                        // Gust of Wind
                        if (HasAura(43621))
                            damage = (int)GetMaxHealth() / 2;

                        int original_health = (int)GetHealth();
                        int final_damage = EnvironmentalDamage(EnviromentalDamage.Fall, damage);

                        // recheck alive, might have died of EnvironmentalDamage, avoid cases when player die in fact like Spirit of Redemption case
                        if (IsAlive() && final_damage < original_health)
                            UpdateCriteria(CriteriaType.MaxDistFallenWithoutDying, (uint)z_diff * 100);
                    }

                    //Z given by moveinfo, LastZ, FallTime, WaterZ, MapZ, Damage, Safefall reduction
                    Log.outDebug(LogFilter.Player, 
                        $"FALLDAMAGE z={movementInfo.Pos.GetPositionZ()} sz={height} pZ={GetPositionZ()} " +
                        $"FallTime={movementInfo.jump.fallTime} mZ={height} damage={damage} SF={safe_fall}\n" +
                        $"Player debug info:\n{GetDebugInfo()}");
                }
            }
        }

        public void UpdateFallInformationIfNeed(MovementInfo minfo, ClientOpcodes opcode)
        {
            if (m_lastFallTime >= m_movementInfo.jump.fallTime || m_lastFallZ <= m_movementInfo.Pos.posZ || opcode == ClientOpcodes.MoveFallLand)
                SetFallInformation(m_movementInfo.jump.fallTime, m_movementInfo.Pos.posZ);
        }

        public bool HasSummonPending()
        {
            return m_summon_expire >= LoopTime.ServerTime;
        }

        public void SendSummonRequestFrom(Unit summoner)
        {
            if (summoner == null)
                return;

            // Player already has active summon request
            if (HasSummonPending())
                return;

            // Evil Twin (ignore player summon, but hide this for summoner)
            if (HasAura(23445))
                return;

            m_summon_expire = LoopTime.ServerTime + PlayerConst.MaxPlayerSummonDelay;
            m_summon_location = new WorldLocation(summoner);
            m_summon_instanceId = summoner.GetInstanceId();

            SummonRequest summonRequest = new();
            summonRequest.SummonerGUID = summoner.GetGUID();
            summonRequest.SummonerVirtualRealmAddress = WorldMgr.GetVirtualRealmAddress();
            summonRequest.AreaID = summoner.GetZoneId();
            SendPacket(summonRequest);

            Group group = GetGroup();
            if (group != null)
            {
                BroadcastSummonCast summonCast = new();
                summonCast.Target = GetGUID();
                group.BroadcastPacket(summonCast, false);
            }
        }

        public bool IsInAreaTriggerRadius(AreaTriggerRecord trigger)
        {
            if (trigger == null)
                return false;

            if (GetMapId() != trigger.ContinentID && !GetPhaseShift().HasVisibleMapId(trigger.ContinentID))
                return false;

            if (trigger.PhaseID != 0 || trigger.PhaseGroupID != 0 || trigger.PhaseUseFlags != 0)
            {
                if (!PhasingHandler.InDbPhaseShift(this, (PhaseUseFlagsValues)trigger.PhaseUseFlags, trigger.PhaseID, trigger.PhaseGroupID))
                    return false;
            }

            if (trigger.Radius > 0.0f)
            {
                // if we have radius check it
                float dist = GetDistance(trigger.Pos.X, trigger.Pos.Y, trigger.Pos.Z);
                if (dist > trigger.Radius)
                    return false;
            }
            else
            {
                Position center = new(trigger.Pos.X, trigger.Pos.Y, trigger.Pos.Z, trigger.BoxYaw);
                if (!IsWithinBox(center, trigger.BoxLength / 2.0f, trigger.BoxWidth / 2.0f, trigger.BoxHeight / 2.0f))
                    return false;
            }

            return true;
        }

        public void SummonIfPossible(bool agree)
        {
            void broadcastSummonResponse(bool accepted)
            {
                Group group = GetGroup();
                if (group != null)
                {
                    BroadcastSummonResponse summonResponse = new();
                    summonResponse.Target = GetGUID();
                    summonResponse.Accepted = accepted;
                    group.BroadcastPacket(summonResponse, false);
                }
            }

            if (!agree)
            {
                m_summon_expire = ServerTime.Zero;
                broadcastSummonResponse(false);
                return;
            }

            // expire and auto declined
            if (m_summon_expire < LoopTime.ServerTime)
            {
                broadcastSummonResponse(false);
                return;
            }

            // stop taxi flight at summon
            FinishTaxiFlight();

            // drop flag at summon
            // this code can be reached only when GM is summoning player who carries flag, because player should be immune to summoning spells when he carries flag
            Battleground bg = GetBattleground();
            if (bg != null)
                bg.EventPlayerDroppedFlag(this);

            m_summon_expire = ServerTime.Zero;

            UpdateCriteria(CriteriaType.AcceptSummon, 1);
            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Summon);

            TeleportTo(m_summon_location, 0, m_summon_instanceId);

            broadcastSummonResponse(true);
        }

        public override void OnPhaseChange()
        {
            base.OnPhaseChange();

            GetMap().UpdatePersonalPhasesForPlayer(this);
        }

        //GM
        public bool IsDeveloper() { return HasPlayerFlag(PlayerFlags.Developer); }

        public void SetDeveloper(bool on)
        {
            if (on)
                SetPlayerFlag(PlayerFlags.Developer);
            else
                RemovePlayerFlag(PlayerFlags.Developer);
        }

        public bool IsAcceptWhispers() { return m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.AcceptWhispers); }

        public void SetAcceptWhispers(bool on)
        {
            if (on)
                m_ExtraFlags |= PlayerExtraFlags.AcceptWhispers;
            else
                m_ExtraFlags &= ~PlayerExtraFlags.AcceptWhispers;
        }

        public bool IsGameMaster() { return m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.GMOn); }
        public bool IsGameMasterAcceptingWhispers() { return IsGameMaster() && IsAcceptWhispers(); }
        public bool CanBeGameMaster() { return GetSession().HasPermission(RBACPermissions.CommandGm); }

        public void SetGameMaster(bool on)
        {
            if (on)
            {
                m_ExtraFlags |= PlayerExtraFlags.GMOn;
                SetFaction(35);
                SetPlayerFlag(PlayerFlags.GM);
                SetUnitFlag2(UnitFlags2.AllowCheatSpells);

                Pet pet = GetPet();
                if (pet != null)
                    pet.SetFaction(35);

                RemovePvpFlag(UnitPVPStateFlags.FFAPvp);
                ResetContestedPvP();

                CombatStopWithPets();

                PhasingHandler.SetAlwaysVisible(this, true, false);
                m_serverSideVisibilityDetect.SetValue(ServerSideVisibilityType.GM, GetSession().GetSecurity());
            }
            else
            {
                PhasingHandler.SetAlwaysVisible(this, HasAuraType(AuraType.PhaseAlwaysVisible), false);

                m_ExtraFlags &= ~PlayerExtraFlags.GMOn;
                RestoreFaction();
                RemovePlayerFlag(PlayerFlags.GM);
                RemoveUnitFlag2(UnitFlags2.AllowCheatSpells);

                Pet pet = GetPet();
                if (pet != null)
                    pet.SetFaction(GetFaction());

                // restore FFA PvP Server state
                if (WorldMgr.IsFFAPvPRealm())
                    SetPvpFlag(UnitPVPStateFlags.FFAPvp);

                // restore FFA PvP area state, remove not allowed for GM mounts
                UpdateArea(m_areaUpdateId);

                m_serverSideVisibilityDetect.SetValue(ServerSideVisibilityType.GM, AccountTypes.Player);
            }

            UpdateObjectVisibility();
        }

        public bool IsGMChat() { return m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.GMChat); }

        public void SetGMChat(bool on)
        {
            if (on)
                m_ExtraFlags |= PlayerExtraFlags.GMChat;
            else
                m_ExtraFlags &= ~PlayerExtraFlags.GMChat;
        }

        public bool IsTaxiCheater() { return m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.TaxiCheat); }

        public void SetTaxiCheater(bool on)
        {
            if (on)
                m_ExtraFlags |= PlayerExtraFlags.TaxiCheat;
            else
                m_ExtraFlags &= ~PlayerExtraFlags.TaxiCheat;
        }

        public bool IsGMVisible() { return !m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.GMInvisible); }

        public void SetGMVisible(bool on)
        {
            if (on)
            {
                m_ExtraFlags &= ~PlayerExtraFlags.GMInvisible;         //remove flag
                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, AccountTypes.Player);
            }
            else
            {
                m_ExtraFlags |= PlayerExtraFlags.GMInvisible;          //add flag

                SetAcceptWhispers(false);
                SetGameMaster(true);

                m_serverSideVisibility.SetValue(ServerSideVisibilityType.GM, GetSession().GetSecurity());
            }

            foreach (Channel channel in m_channels)
                channel.SetInvisible(this, !on);
        }

        //Chat - Text - Channel
        public void PrepareGossipMenu(WorldObject source, int menuId, bool showQuests = false)
        {
            PlayerMenu menu = PlayerTalkClass;
            menu.ClearMenus();

            menu.GetGossipMenu().SetMenuId(menuId);

            var menuItemBounds = ObjectMgr.GetGossipMenuItemsMapBounds(menuId);

            if (source.IsTypeId(TypeId.Unit))
            {
                if (showQuests && source.ToUnit().IsQuestGiver())
                    PrepareQuestMenu(source.GetGUID());
            }
            else if (source.IsTypeId(TypeId.GameObject))
            {
                if (source.ToGameObject().GetGoType() == GameObjectTypes.QuestGiver)
                    PrepareQuestMenu(source.GetGUID());
            }

            foreach (var gossipMenuItem in menuItemBounds)
            {
                if (!gossipMenuItem.Conditions.Meets(this, source))
                    continue;

                bool canTalk = true;
                GameObject go = source.ToGameObject();
                Creature creature = source.ToCreature();
                if (creature != null)
                {
                    switch (gossipMenuItem.OptionNpc)
                    {
                        case GossipOptionNpc.Taxinode:
                            if (GetSession().SendLearnNewTaxiNode(creature))
                                return;
                            break;
                        case GossipOptionNpc.SpiritHealer:
                            if (!IsDead())
                                canTalk = false;
                            break;
                        case GossipOptionNpc.Battlemaster:
                            if (!creature.CanInteractWithBattleMaster(this, false))
                                canTalk = false;
                            break;
                        case GossipOptionNpc.TalentMaster:
                        case GossipOptionNpc.SpecializationMaster:
                        case GossipOptionNpc.GlyphMaster:
                            if (!creature.CanResetTalents(this))
                                canTalk = false;
                            break;
                        case GossipOptionNpc.Stablemaster:
                        case GossipOptionNpc.PetSpecializationMaster:
                            if (GetClass() != Class.Hunter)
                                canTalk = false;
                            break;
                        case GossipOptionNpc.DisableXPGain:
                            if (HasPlayerFlag(PlayerFlags.NoXPGain) || IsMaxLevel())
                                canTalk = false;
                            break;
                        case GossipOptionNpc.EnableXPGain:
                            if (!HasPlayerFlag(PlayerFlags.NoXPGain) || IsMaxLevel())
                                canTalk = false;
                            break;
                        case GossipOptionNpc.None:
                        case GossipOptionNpc.Vendor:
                        case GossipOptionNpc.Trainer:
                        case GossipOptionNpc.Binder:
                        case GossipOptionNpc.Banker:
                        case GossipOptionNpc.PetitionVendor:
                        case GossipOptionNpc.GuildTabardVendor:
                        case GossipOptionNpc.Auctioneer:
                        case GossipOptionNpc.Mailbox:
                        case GossipOptionNpc.Transmogrify:
                        case GossipOptionNpc.AzeriteRespec:
                        case GossipOptionNpc.PersonalTabardVendor:
                            break;                                         // No checks
                        case GossipOptionNpc.CemeterySelect:
                            canTalk = false;                               // Deprecated
                            break;
                        default:
                            if (gossipMenuItem.OptionNpc >= GossipOptionNpc.Max)
                            {
                                Log.outError(LogFilter.Sql, 
                                    $"Creature entry {creature.GetEntry()} " +
                                    $"has an unknown gossip option icon {gossipMenuItem.OptionNpc} " +
                                    $"for menu {gossipMenuItem.MenuID}.");

                                canTalk = false;
                            }
                            break;                                         // NYI
                    }
                }
                else if (go != null)
                {
                    switch (gossipMenuItem.OptionNpc)
                    {
                        case GossipOptionNpc.None:
                            if (go.GetGoType() != GameObjectTypes.QuestGiver && go.GetGoType() != GameObjectTypes.Goober)
                                canTalk = false;
                            break;
                        default:
                            canTalk = false;
                            break;
                    }
                }

                if (canTalk)
                    menu.GetGossipMenu().AddMenuItem(gossipMenuItem, gossipMenuItem.MenuID, gossipMenuItem.OrderIndex);
            }
        }

        public void SendPreparedGossip(WorldObject source)
        {
            if (source == null)
                return;

            if (source.IsTypeId(TypeId.Unit) || source.IsTypeId(TypeId.GameObject))
            {
                if (PlayerTalkClass.GetGossipMenu().IsEmpty() && !PlayerTalkClass.GetQuestMenu().IsEmpty())
                {
                    SendPreparedQuest(source);
                    return;
                }
            }

            // in case non empty gossip menu (that not included quests list size) show it
            // (quest entries from quest menu will be included in list)

            int textId = GetGossipTextId(source);
            int menuId = PlayerTalkClass.GetGossipMenu().GetMenuId();
            if (menuId != 0)
                textId = GetGossipTextId(menuId, source);

            PlayerTalkClass.SendGossipMenu(textId, source.GetGUID());
        }

        public void OnGossipSelect(WorldObject source, int gossipOptionId, int menuId)
        {
            GossipMenu gossipMenu = PlayerTalkClass.GetGossipMenu();

            // if not same, then something funky is going on
            if (menuId != gossipMenu.GetMenuId())
                return;

            GossipMenuItem item = gossipMenu.GetItem(gossipOptionId);
            if (item == null)
                return;

            GossipOptionNpc gossipOptionNpc = item.OptionNpc;
            ObjectGuid guid = source.GetGUID();

            if (source.IsTypeId(TypeId.GameObject))
            {
                if (gossipOptionNpc != GossipOptionNpc.None)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player guid {GetGUID()} request invalid gossip option " +
                        $"for GameObject entry {source.GetEntry()}");
                    return;
                }
            }

            long cost = item.BoxMoney;
            if (!HasEnoughMoney(cost))
            {
                SendBuyError(BuyResult.NotEnoughtMoney, null, 0);
                PlayerTalkClass.SendCloseGossip();
                return;
            }

            if (item.ActionPoiID != 0)
                PlayerTalkClass.SendPointOfInterest(item.ActionPoiID);

            if (item.ActionMenuID != 0)
            {
                PrepareGossipMenu(source, item.ActionMenuID);
                SendPreparedGossip(source);
            }

            // types that have their dedicated open opcode dont send WorldPackets::NPC::GossipOptionNPCInteraction
            bool handled = true;
            switch (gossipOptionNpc)
            {
                case GossipOptionNpc.None:
                    break;
                case GossipOptionNpc.Vendor:
                    GetSession().SendListInventory(guid);
                    break;
                case GossipOptionNpc.Taxinode:
                    GetSession().SendTaxiMenu(source.ToCreature());
                    break;
                case GossipOptionNpc.Trainer:
                    GetSession().SendTrainerList(source.ToCreature(), ObjectMgr.GetCreatureTrainerForGossipOption(source.GetEntry(), menuId, item.OrderIndex));
                    break;
                case GossipOptionNpc.SpiritHealer:
                    source.CastSpell(source.ToCreature(), 17251, new CastSpellExtraArgs(TriggerCastFlags.FullMask).SetOriginalCaster(GetGUID()));
                    handled = false;
                    break;
                case GossipOptionNpc.PetitionVendor:
                    PlayerTalkClass.SendCloseGossip();
                    GetSession().SendPetitionShowList(guid);
                    break;
                case GossipOptionNpc.Battlemaster:
                {
                    BattlegroundTypeId bgTypeId = BattlegroundMgr.GetBattleMasterBG(source.GetEntry());

                    if (bgTypeId == BattlegroundTypeId.None)
                    {
                        Log.outError(LogFilter.Player, 
                            $"a user (guid {GetGUID()}) requested Battlegroundlist from a npc who is no battlemaster");
                        return;
                    }

                    BattlegroundMgr.SendBattlegroundList(this, guid, bgTypeId);
                    break;
                }
                case GossipOptionNpc.Auctioneer:
                    GetSession().SendAuctionHello(guid, source.ToCreature());
                    break;
                case GossipOptionNpc.TalentMaster:
                    PlayerTalkClass.SendCloseGossip();
                    SendRespecWipeConfirm(guid, WorldConfig.Values[WorldCfg.NoResetTalentCost].Bool ? 0 : GetNextResetTalentsCost(), SpecResetType.Talents);
                    break;
                case GossipOptionNpc.Stablemaster:
                    SetStableMaster(guid);
                    handled = false;
                    break;
                case GossipOptionNpc.PetSpecializationMaster:
                    PlayerTalkClass.SendCloseGossip();
                    SendRespecWipeConfirm(guid, WorldConfig.Values[WorldCfg.NoResetTalentCost].Bool ? 0 : GetNextResetTalentsCost(), SpecResetType.PetTalents);
                    break;
                case GossipOptionNpc.GuildBanker:
                    Guild guild = GetGuild();
                    if (guild != null)
                        guild.SendBankList(GetSession(), 0, true);
                    else
                        Guild.SendCommandResult(GetSession(), GuildCommandType.ViewTab, GuildCommandError.PlayerNotInGuild);
                    break;
                case GossipOptionNpc.Spellclick:
                    Unit sourceUnit = source.ToUnit();
                    if (sourceUnit != null)
                        sourceUnit.HandleSpellClick(this);
                    break;
                case GossipOptionNpc.DisableXPGain:
                    PlayerTalkClass.SendCloseGossip();
                    CastSpell(null, PlayerConst.SpellExperienceEliminated, true);
                    SetPlayerFlag(PlayerFlags.NoXPGain);
                    break;
                case GossipOptionNpc.EnableXPGain:
                    PlayerTalkClass.SendCloseGossip();
                    RemoveAurasDueToSpell(PlayerConst.SpellExperienceEliminated);
                    RemovePlayerFlag(PlayerFlags.NoXPGain);
                    break;
                case GossipOptionNpc.SpecializationMaster:
                    PlayerTalkClass.SendCloseGossip();
                    SendRespecWipeConfirm(guid, 0, SpecResetType.Specialization);
                    break;
                case GossipOptionNpc.GlyphMaster:
                    PlayerTalkClass.SendCloseGossip();
                    SendRespecWipeConfirm(guid, 0, SpecResetType.Glyphs);
                    break;
                case GossipOptionNpc.GarrisonTradeskillNpc: // NYI
                    break;
                case GossipOptionNpc.GarrisonRecruitment: // NYI
                    break;
                case GossipOptionNpc.ChromieTimeNpc: // NYI
                    break;
                case GossipOptionNpc.RuneforgeLegendaryCrafting: // NYI
                    break;
                case GossipOptionNpc.RuneforgeLegendaryUpgrade: // NYI
                    break;
                case GossipOptionNpc.ProfessionsCraftingOrder: // NYI
                    break;
                case GossipOptionNpc.ProfessionsCustomerOrder: // NYI
                    break;
                case GossipOptionNpc.BarbersChoice: // NYI - unknown if needs sending
                default:
                    handled = false;
                    break;
            }

            if (!handled)
            {
                if (item.GossipNpcOptionID.HasValue)
                {
                    GossipMenuAddon addon = ObjectMgr.GetGossipMenuAddon(menuId);

                    GossipOptionNPCInteraction npcInteraction = new();
                    npcInteraction.GossipGUID = source.GetGUID();
                    npcInteraction.GossipNpcOptionID = item.GossipNpcOptionID.Value;
                    if (addon != null && addon.FriendshipFactionID != 0)
                        npcInteraction.FriendshipFactionID = addon.FriendshipFactionID;

                    SendPacket(npcInteraction);
                }
                else
                {
                    PlayerInteractionType[] GossipOptionNpcToInteractionType =
                    [
                        PlayerInteractionType.None, PlayerInteractionType.Vendor, PlayerInteractionType.TaxiNode,
                        PlayerInteractionType.Trainer, PlayerInteractionType.SpiritHealer, PlayerInteractionType.Binder,
                        PlayerInteractionType.Banker, PlayerInteractionType.PetitionVendor, PlayerInteractionType.GuildTabardVendor,
                        PlayerInteractionType.BattleMaster, PlayerInteractionType.Auctioneer, PlayerInteractionType.TalentMaster,
                        PlayerInteractionType.StableMaster, PlayerInteractionType.None, PlayerInteractionType.GuildBanker,
                        PlayerInteractionType.None, PlayerInteractionType.None, PlayerInteractionType.None,
                        PlayerInteractionType.MailInfo, PlayerInteractionType.None, PlayerInteractionType.LFGDungeon,
                        PlayerInteractionType.ArtifactForge, PlayerInteractionType.None, PlayerInteractionType.SpecializationMaster,
                        PlayerInteractionType.None, PlayerInteractionType.None, PlayerInteractionType.GarrArchitect,
                        PlayerInteractionType.GarrMission, PlayerInteractionType.ShipmentCrafter, PlayerInteractionType.GarrTradeskill,
                        PlayerInteractionType.GarrRecruitment, PlayerInteractionType.AdventureMap, PlayerInteractionType.GarrTalent,
                        PlayerInteractionType.ContributionCollector, PlayerInteractionType.Transmogrifier, PlayerInteractionType.AzeriteRespec,
                        PlayerInteractionType.IslandQueue, PlayerInteractionType.ItemInteraction, PlayerInteractionType.WorldMap,
                        PlayerInteractionType.Soulbind, PlayerInteractionType.ChromieTime, PlayerInteractionType.CovenantPreview,
                        PlayerInteractionType.LegendaryCrafting, PlayerInteractionType.NewPlayerGuide, PlayerInteractionType.LegendaryCrafting,
                        PlayerInteractionType.Renown, PlayerInteractionType.BlackMarketAuctioneer, PlayerInteractionType.PerksProgramVendor,
                        PlayerInteractionType.ProfessionsCraftingOrder, PlayerInteractionType.Professions, PlayerInteractionType.ProfessionsCustomerOrder,
                        PlayerInteractionType.TraitSystem, PlayerInteractionType.BarbersChoice, PlayerInteractionType.MajorFactionRenown,
                        PlayerInteractionType.PersonalTabardVendor
                    ];

                    PlayerInteractionType interactionType = GossipOptionNpcToInteractionType[(int)gossipOptionNpc];
                    if (interactionType != PlayerInteractionType.None)
                    {
                        NPCInteractionOpenResult npcInteraction = new();
                        npcInteraction.Npc = source.GetGUID();
                        npcInteraction.InteractionType = interactionType;
                        npcInteraction.Success = true;
                        SendPacket(npcInteraction);
                    }
                }
            }

            ModifyMoney(-cost);
        }

        public int GetGossipTextId(WorldObject source)
        {
            if (source == null)
                return SharedConst.DefaultGossipMessage;

            return GetGossipTextId(GetGossipMenuForSource(source), source);
        }

        public int GetGossipTextId(int menuId, WorldObject source)
        {
            int textId = SharedConst.DefaultGossipMessage;

            if (menuId == 0)
                return textId;

            var menuBounds = ObjectMgr.GetGossipMenusMapBounds(menuId);

            foreach (var menu in menuBounds)
            {
                // continue if only checks menuid instead of text
                if (menu.TextId == 0)
                    continue;

                if (menu.Conditions.Meets(this, source))
                    textId = menu.TextId;
            }

            return textId;
        }

        public int GetGossipMenuForSource(WorldObject source)
        {
            switch (source.GetTypeId())
            {
                case TypeId.Unit:
                {
                    var menuIdToShow = source.ToCreature().GetGossipMenuId();

                    // if menu id is set by script
                    if (menuIdToShow != 0)
                        return menuIdToShow;

                    // otherwise pick from db based on conditions
                    foreach (var menuId in source.ToCreature().GetCreatureTemplate().GossipMenuIds)
                    {
                        var menuBounds = ObjectMgr.GetGossipMenusMapBounds(menuId);

                        foreach (var menu in menuBounds)
                        {
                            if (!menu.Conditions.Meets(this, source))
                                continue;

                            menuIdToShow = menuId;
                        }
                    }
                    return menuIdToShow;
                }
                case TypeId.GameObject:
                    return source.ToGameObject().GetGoInfo().GetGossipMenuId();
                default:
                    break;
            }

            return 0;
        }

        public bool CanJoinConstantChannelInZone(ChatChannelsRecord channel, AreaTableRecord zone)
        {
            if (channel.HasFlag(ChatChannelFlags.ZoneBased) && zone.HasFlag(AreaFlags.NoChatChannels))
                return false;

            if (channel.HasFlag(ChatChannelFlags.OnlyInCities) && !zone.HasFlag(AreaFlags.AllowTradeChannel))
                return false;

            if (channel.HasFlag(ChatChannelFlags.GuildRecruitment) && GetGuildId() != 0)
                return false;

            if (channel.Ruleset == ChatChannelRuleset.Disabled)
                return false;

            if (channel.HasFlag(ChatChannelFlags.Regional))
                return false;

            return true;
        }

        public void JoinedChannel(Channel c)
        {
            m_channels.Add(c);
        }

        public void LeftChannel(Channel c)
        {
            m_channels.Remove(c);
        }

        public void CleanupChannels()
        {
            while (!m_channels.Empty())
            {
                Channel ch = m_channels.FirstOrDefault();
                m_channels.RemoveAt(0);               // remove from player's channel list
                ch.LeaveChannel(this, false);                     // not send to client, not remove from player's channel list

                // delete channel if empty
                ChannelManager cMgr = ChannelManager.ForTeam(GetTeam());
                if (cMgr != null)
                    if (ch.IsConstant())
                        cMgr.LeftChannel(ch.GetChannelId(), ch.GetZoneEntry());
            }
            Log.outDebug(LogFilter.ChatSystem, $"Player {GetName()}: channels cleaned up!");
        }

        void UpdateLocalChannels(int newZone)
        {
            if (GetSession().PlayerLoading() && !IsBeingTeleportedFar())
                return;                                              // The client handles it automatically after loading, but not after teleporting

            AreaTableRecord current_zone = CliDB.AreaTableStorage.LookupByKey(newZone);
            if (current_zone == null)
                return;

            ChannelManager cMgr = ChannelManager.ForTeam(GetTeam());
            if (cMgr == null)
                return;

            foreach (var channelEntry in CliDB.ChatChannelsStorage.Values)
            {
                if (!channelEntry.HasFlag(ChatChannelFlags.AutoJoin))
                    continue;

                Channel usedChannel = null;
                foreach (var channel in m_channels)
                {
                    if (channel.GetChannelId() == channelEntry.Id)
                    {
                        usedChannel = channel;
                        break;
                    }
                }

                Channel removeChannel = null;
                Channel joinChannel = null;
                bool sendRemove = true;

                if (CanJoinConstantChannelInZone(channelEntry, current_zone))
                {
                    if (!channelEntry.HasFlag(ChatChannelFlags.ZoneBased))
                    {
                        if (channelEntry.HasFlag(ChatChannelFlags.LinkedChannel) && usedChannel != null)
                            continue;                            // Already on the channel, as city channel names are not changing

                        joinChannel = cMgr.GetSystemChannel(channelEntry.Id, current_zone);
                        if (usedChannel != null)
                        {
                            if (joinChannel != usedChannel)
                            {
                                removeChannel = usedChannel;
                                sendRemove = false;              // Do not send leave channel, it already replaced at client
                            }
                            else
                                joinChannel = null;
                        }
                    }
                    else
                        joinChannel = cMgr.GetSystemChannel(channelEntry.Id);
                }
                else
                    removeChannel = usedChannel;

                if (joinChannel != null)
                    joinChannel.JoinChannel(this);          // Changed Channel: ... or Joined Channel: ...

                if (removeChannel != null)
                {
                    removeChannel.LeaveChannel(this, sendRemove, true); // Leave old channel

                    LeftChannel(removeChannel);                  // Remove from player's channel list
                    cMgr.LeftChannel(removeChannel.GetChannelId(), removeChannel.GetZoneEntry());                     // Delete if empty
                }
            }
        }

        public List<Channel> GetJoinedChannels() { return m_channels; }

        //Mail
        public void AddMail(Mail mail) { m_mail.Insert(0, mail); }

        public void RemoveMail(long id)
        {
            foreach (var mail in m_mail)
            {
                if (mail.messageID == id)
                {
                    //do not delete item, because Player.removeMail() is called when returning mail to sender.
                    m_mail.Remove(mail);
                    return;
                }
            }
        }

        public void SendMailResult(long mailId, MailResponseType mailAction, MailResponseResult mailError, InventoryResult equipError = 0, long itemGuid = 0, int itemCount = 0)
        {
            MailCommandResult result = new();
            result.MailID = mailId;
            result.Command = (int)mailAction;
            result.ErrorCode = (int)mailError;

            if (mailError == MailResponseResult.EquipError)
                result.BagResult = (int)equipError;
            else if (mailAction == MailResponseType.ItemTaken)
            {
                result.AttachID = itemGuid;
                result.QtyInInventory = itemCount;
            }

            SendPacket(result);
        }

        void SendNewMail()
        {
            SendPacket(new NotifyReceivedMail());
        }

        public void UpdateNextMailTimeAndUnreads()
        {
            // calculate next delivery time (min. from non-delivered mails
            // and recalculate unReadMail
            ServerTime cTime = LoopTime.ServerTime;
            m_nextMailDelivereTime = ServerTime.Zero;
            unReadMails = 0;
            foreach (var mail in m_mail)
            {
                if (mail.deliver_time > cTime)
                {
                    if (m_nextMailDelivereTime == ServerTime.Zero || m_nextMailDelivereTime > mail.deliver_time)
                        m_nextMailDelivereTime = mail.deliver_time;
                }
                else if ((mail.checkMask & MailCheckFlags.Read) == 0)
                    ++unReadMails;
            }
        }

        public void AddNewMailDeliverTime(ServerTime deliver_time)
        {
            if (deliver_time <= LoopTime.ServerTime)                          // ready now
            {
                ++unReadMails;
                SendNewMail();
            }
            else                                                    // not ready and no have ready mails
            {
                if (m_nextMailDelivereTime == ServerTime.Zero || m_nextMailDelivereTime > deliver_time)
                    m_nextMailDelivereTime = deliver_time;
            }
        }

        public void AddMItem(Item it)
        {
            mMitems[it.GetGUID().GetCounter()] = it;
        }

        public bool RemoveMItem(long id)
        {
            return mMitems.Remove(id);
        }

        public Item GetMItem(long id) { return mMitems.LookupByKey(id); }
        public Mail GetMail(long id) { return m_mail.Find(p => p.messageID == id); }
        public List<Mail> GetMails() { return m_mail; }
        public uint GetMailSize() { return (uint)m_mail.Count; }

        //Binds
        public bool HasPendingBind() { return _pendingBindId > 0; }

        public static readonly Seconds HomeBindTime = (Minutes)1;

        void UpdateHomebindTime(Milliseconds diff)
        {
            // GMs never get homebind timer online
            if (m_InstanceValid || IsGameMaster())
            {
                if (m_HomebindTimer != 0) // instance valid, but timer not reset
                    SendRaidGroupOnlyMessage(RaidGroupReason.None, Milliseconds.Zero);

                // instance is valid, reset homebind timer
                m_HomebindTimer = Milliseconds.Zero;
            }
            else if (m_HomebindTimer > 0)
            {
                if (diff >= m_HomebindTimer)
                {
                    // teleport to nearest graveyard
                    RepopAtGraveyard();
                }
                else
                    m_HomebindTimer -= diff;
            }
            else
            {
                // instance is invalid, start homebind timer
                m_HomebindTimer = HomeBindTime;
                // send message to player
                SendRaidGroupOnlyMessage(RaidGroupReason.RequirementsUnmatch, m_HomebindTimer);

                Log.outDebug(LogFilter.Maps, 
                    $"PLAYER: Player '{GetName()}' (GUID: {GetGUID()}) will be teleported to homebind in {HomeBindTime} seconds.");
            }
        }

        public void SetHomebind(WorldLocation loc, int areaId)
        {
            homebind.WorldRelocate(loc);
            homebindAreaId = areaId;

            // update sql homebind
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_PLAYER_HOMEBIND);
            stmt.SetInt32(0, homebind.GetMapId());
            stmt.SetInt32(1, homebindAreaId);
            stmt.SetFloat(2, homebind.GetPositionX());
            stmt.SetFloat(3, homebind.GetPositionY());
            stmt.SetFloat(4, homebind.GetPositionZ());
            stmt.SetFloat(5, homebind.GetOrientation());
            stmt.SetInt64(6, GetGUID().GetCounter());
            DB.Characters.Execute(stmt);
        }

        public void SetBindPoint(ObjectGuid guid)
        {
            NPCInteractionOpenResult npcInteraction = new();
            npcInteraction.Npc = guid;
            npcInteraction.InteractionType = PlayerInteractionType.Binder;
            npcInteraction.Success = true;
            SendPacket(npcInteraction);
        }

        public void SendBindPointUpdate()
        {
            BindPointUpdate packet = new();
            packet.BindPosition = new(homebind.GetPositionX(), homebind.GetPositionY(), homebind.GetPositionZ());
            packet.BindMapID = homebind.GetMapId();
            packet.BindAreaID = homebindAreaId;
            SendPacket(packet);
        }

        public void SendPlayerBound(ObjectGuid binderGuid, int areaId)
        {
            PlayerBound packet = new(binderGuid, areaId);
            SendPacket(packet);
        }

        //Misc
        public Seconds GetTotalPlayedTime() { return m_PlayedTimeTotal; }
        public Seconds GetLevelPlayedTime() { return m_PlayedTimeLevel; }

        public CinematicManager GetCinematicMgr() { return _cinematicMgr; }

        public void SendUpdateWorldState(int variable, WorldStateValue value, bool hidden = false)
        {
            UpdateWorldState worldstate = new();
            worldstate.VariableID = variable;
            worldstate.Value = value;
            worldstate.Hidden = hidden;
            SendPacket(worldstate);
        }

        void SendInitWorldStates(int zoneId, int areaId)
        {
            // data depends on zoneid/mapid...
            int mapid = GetMapId();

            InitWorldStates packet = new();
            packet.MapID = mapid;
            packet.AreaID = zoneId;
            packet.SubareaID = areaId;

            WorldStateMgr.FillInitialWorldStates(packet, GetMap(), areaId);

            SendPacket(packet);
        }

        public long GetBarberShopCost(List<ChrCustomizationChoice> newCustomizations)
        {
            if (HasAuraType(AuraType.RemoveBarberShopCost))
                return 0;

            GtBarberShopCostBaseRecord bsc = CliDB.BarberShopCostBaseGameTable.GetRow(GetLevel());
            if (bsc == null)                                                // shouldn't happen
                return 0;

            long cost = 0;
            foreach (ChrCustomizationChoice newChoice in newCustomizations)
            {
                int currentCustomizationIndex = m_playerData.Customizations.FindIndexIf(currentCustomization =>
                {
                    return currentCustomization.ChrCustomizationOptionID == newChoice.ChrCustomizationOptionID;
                });

                if (currentCustomizationIndex == -1 || m_playerData.Customizations[currentCustomizationIndex].ChrCustomizationChoiceID != newChoice.ChrCustomizationChoiceID)
                {
                    ChrCustomizationOptionRecord customizationOption = CliDB.ChrCustomizationOptionStorage.LookupByKey(newChoice.ChrCustomizationOptionID);
                    if (customizationOption != null)
                        cost += (long)(bsc.Cost * customizationOption.BarberShopCostModifier);
                }
            }

            return cost;
        }

        int GetChampioningFaction() { return m_ChampioningFaction; }

        public void SetChampioningFaction(int faction) { m_ChampioningFaction = faction; }

        public static byte GetFactionGroupForRace(Race race)
        {
            var rEntry = CliDB.ChrRacesStorage.LookupByKey((int)race);
            if (rEntry != null)
            {
                var faction = CliDB.FactionTemplateStorage.LookupByKey(rEntry.FactionID);
                if (faction != null)
                    return faction.FactionGroup;
            }

            return 1;
        }

        public void SetFactionForRace(Race race)
        {
            m_team = TeamForRace(race);

            ChrRacesRecord rEntry = CliDB.ChrRacesStorage.LookupByKey((int)race);
            SetFaction(rEntry != null ? rEntry.FactionID : 0);
        }

        public void SetResurrectRequestData(WorldObject caster, int health, int mana, int appliedAura)
        {
            Cypher.Assert(!IsResurrectRequested());
            _resurrectionData = new ResurrectionData();
            _resurrectionData.GUID = caster.GetGUID();
            _resurrectionData.Location.WorldRelocate(caster);
            _resurrectionData.Health = health;
            _resurrectionData.Mana = mana;
            _resurrectionData.Aura = appliedAura;
        }

        public void ClearResurrectRequestData()
        {
            _resurrectionData = null;
        }

        public bool IsRessurectRequestedBy(ObjectGuid guid)
        {
            if (!IsResurrectRequested())
                return false;

            return !_resurrectionData.GUID.IsEmpty() && _resurrectionData.GUID == guid;
        }

        public bool IsResurrectRequested() { return _resurrectionData != null; }

        public void ResurrectUsingRequestData()
        {
            // Teleport before resurrecting by player, otherwise the player might get attacked from creatures near his corpse
            TeleportTo(_resurrectionData.Location);

            if (IsBeingTeleported())
            {
                ScheduleDelayedOperation(PlayerDelayedOperations.ResurrectPlayer);
                return;
            }

            ResurrectUsingRequestDataImpl();
        }

        void ResurrectUsingRequestDataImpl()
        {
            // save health and mana before resurrecting, _resurrectionData can be erased
            int resurrectHealth = _resurrectionData.Health;
            int resurrectMana = _resurrectionData.Mana;
            int resurrectAura = _resurrectionData.Aura;
            ObjectGuid resurrectGUID = _resurrectionData.GUID;

            ResurrectPlayer(0.0f, false);

            SetHealth(resurrectHealth);
            SetPower(PowerType.Mana, resurrectMana);

            SetPower(PowerType.Rage, 0);
            SetFullPower(PowerType.Energy);
            SetFullPower(PowerType.Focus);

            if (resurrectAura != 0)
                CastSpell(this, resurrectAura, new CastSpellExtraArgs(TriggerCastFlags.FullMask).SetOriginalCaster(resurrectGUID));

            SpawnCorpseBones();
        }

        public void UpdateTriggerVisibility()
        {
            if (m_clientGUIDs.Empty())
                return;

            if (!IsInWorld)
                return;

            UpdateData udata = new(GetMapId());
            foreach (var guid in m_clientGUIDs)
            {
                if (guid.IsCreatureOrVehicle())
                {
                    Creature creature = GetMap().GetCreature(guid);
                    // Update fields of triggers, transformed units or unselectable units (values dependent on GM state)
                    if (creature == null || (!creature.IsTrigger() && !creature.HasAuraType(AuraType.Transform) && !creature.IsUninteractible()))
                        continue;

                    creature.m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.DisplayID);
                    creature.m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Flags);
                    creature.ForceUpdateFieldChange();
                    creature.BuildValuesUpdateBlockForPlayer(udata, this);
                }
                else if (guid.IsAnyTypeGameObject())
                {
                    GameObject go = GetMap().GetGameObject(guid);
                    if (go == null)
                        continue;

                    go.m_values.ModifyValue(m_objectData).ModifyValue(m_objectData.DynamicFlags);
                    go.ForceUpdateFieldChange();
                    go.BuildValuesUpdateBlockForPlayer(udata, this);
                }
            }

            if (!udata.HasData())
                return;

            UpdateObject packet;
            udata.BuildPacket(out packet);
            SendPacket(packet);
        }

        public bool IsAllowedToLoot(Creature creature)
        {
            if (!creature.IsDead())
                return false;

            if (HasPendingBind())
                return false;

            Loot loot = creature.GetLootForPlayer(this);
            if (loot == null || loot.IsLooted()) // nothing to loot or everything looted.
                return false;

            if (!loot.HasAllowedLooter(GetGUID()) || (!loot.HasItemForAll() && !loot.HasItemFor(this))) // no loot in creature for this player
                return false;

            switch (loot.GetLootMethod())
            {
                case LootMethod.PersonalLoot:
                case LootMethod.FreeForAll:
                    return true;
                case LootMethod.RoundRobin:
                    // may only loot if the player is the loot roundrobin player
                    // or if there are free/quest/conditional item for the player
                    if (loot.roundRobinPlayer.IsEmpty() || loot.roundRobinPlayer == GetGUID())
                        return true;

                    return loot.HasItemFor(this);
                case LootMethod.MasterLoot:
                case LootMethod.GroupLoot:
                case LootMethod.NeedBeforeGreed:
                    // may only loot if the player is the loot roundrobin player
                    // or item over threshold (so roll(s) can be launched or to preview master looted items)
                    // or if there are free/quest/conditional item for the player
                    if (loot.roundRobinPlayer.IsEmpty() || loot.roundRobinPlayer == GetGUID())
                        return true;

                    if (loot.HasOverThresholdItem())
                        return true;

                    return loot.HasItemFor(this);
            }

            return false;
        }

        public override bool IsImmunedToSpellEffect(SpellInfo spellInfo, SpellEffectInfo spellEffectInfo, WorldObject caster, bool requireImmunityPurgesEffectAttribute = false)
        {
            // players are immune to taunt (the aura and the spell effect).
            if (spellEffectInfo.IsAura(AuraType.ModTaunt))
                return true;

            if (spellEffectInfo.IsEffect(SpellEffectName.AttackMe))
                return true;

            return base.IsImmunedToSpellEffect(spellInfo, spellEffectInfo, caster, requireImmunityPurgesEffectAttribute);
        }

        void RegenerateAll()
        {
            m_regenTimerCount += RegenTimer;
            m_foodEmoteTimerCount += RegenTimer;

            Regenerate(PowerType.Energy);
            Regenerate(PowerType.Mana);
            Regenerate(PowerType.Rage);
            Regenerate(PowerType.RunicPower);

            if (m_regenTimerCount >= SharedConst.PlayerRegenInterval)
            {
                // Not in combat or they have regeneration
                if (!IsInCombat() || IsPolymorphed() || m_baseHealthRegen != 0 ||
                HasAuraType(AuraType.ModRegenDuringCombat) ||
                HasAuraType(AuraType.ModHealthRegenInCombat))
                {
                    RegenerateHealth();
                }

                m_regenTimerCount -= SharedConst.PlayerRegenInterval;
            }

            RegenTimer = Milliseconds.Zero;

            // Handles the emotes for drinking and eating.
            // According to sniffs there is a background timer going on that repeats independed from the time window where the aura applies.
            // That's why we dont need to reset the timer on apply. In sniffs I have seen that the first call for the spell visual is totally random, then after
            // 5 seconds over and over again which confirms my theory that we have a independed timer.
            if (m_foodEmoteTimerCount >= SharedConst.FoodEmoteInterval)
            {
                List<AuraEffect> auraList = GetAuraEffectsByTypeCopy(AuraType.ModHealthRegen);
                auraList.AddRange(GetAuraEffectsByType(AuraType.ModPowerRegen));

                foreach (var auraEffect in auraList)
                {
                    // Food emote comes above drinking emote if we have to decide (mage regen food for example)
                    if (auraEffect.GetBase().HasEffectType(AuraType.ModHealthRegen) && auraEffect.GetSpellInfo().HasAuraInterruptFlag(SpellAuraInterruptFlags.Standing))
                    {
                        SendPlaySpellVisualKit(SpellConst.VisualKitFood, 0, 0);
                        break;
                    }
                    else if (auraEffect.GetBase().HasEffectType(AuraType.ModPowerRegen) && auraEffect.GetSpellInfo().HasAuraInterruptFlag(SpellAuraInterruptFlags.Standing))
                    {
                        SendPlaySpellVisualKit(SpellConst.VisualKitDrink, 0, 0);
                        break;
                    }
                }
                m_foodEmoteTimerCount -= SharedConst.FoodEmoteInterval;
            }
        }

        void Regenerate(PowerType power)
        {
            // Skip regeneration for power Type we cannot have
            var powerIndex = GetPowerIndex(power);
            if (powerIndex == (int)PowerType.Max || powerIndex >= (int)PowerType.MaxPerClass)
                return;

            int curValue = GetPower(power);

            PowerTypeRecord powerInfo = DB2Mgr.GetPowerTypeEntry(power);
            if (powerInfo == null)
                return;

            if (powerInfo.HasFlag(PowerTypeFlags.UseRegenInterrupt) && m_regenInterruptTimestamp + powerInfo.RegenInterruptTime >= LoopTime.ServerTime)
                return;

            float addvalue = 0;

            if (power == PowerType.Mana)
            {
                if (IsPowerRegenInterruptedByMP5Rule())
                    addvalue = (powerInfo.RegenCombat + m_unitData.PowerRegenInterruptedFlatModifier[powerIndex]) * 0.001f * RegenTimer;
                else
                    addvalue = (powerInfo.RegenPeace + m_unitData.PowerRegenFlatModifier[powerIndex]) * 0.001f * RegenTimer;
            }
            else
            {
                if (IsInCombat())
                    addvalue = (powerInfo.RegenCombat + m_unitData.PowerRegenInterruptedFlatModifier[powerIndex]) * 0.001f * RegenTimer;
                else
                    addvalue = (powerInfo.RegenPeace + m_unitData.PowerRegenFlatModifier[powerIndex]) * 0.001f * RegenTimer;
            }

            float resultValue = curValue + addvalue + m_powerFraction[powerIndex];
            int minPower = powerInfo.MinPower;
            int maxPower = m_unitData.MaxPower[powerIndex];

            if (curValue <= minPower && addvalue <= 0)
                return;

            if (curValue >= maxPower && addvalue >= 0)
                return;

            if (resultValue <= minPower)
            {
                resultValue = minPower;
                m_powerFraction[powerIndex] = 0;
            }
            else if (resultValue >= maxPower)
            {
                resultValue = maxPower;
                m_powerFraction[powerIndex] = 0;
            }
            else
            {
                m_powerFraction[powerIndex] = resultValue - (int)resultValue;
            }

            if (m_regenTimerCount >= SharedConst.PlayerRegenInterval)
            {
                SetPower(power, powerIndex, (int)resultValue);
            }
            else
            {
                // throttle packet sending
                DoWithSuppressingObjectUpdates(() =>
                {
                    SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Power, powerIndex), (int)resultValue);
                    m_unitData.ClearChanged(m_unitData.Power, powerIndex);
                });
            }
        }

        public void InterruptPowerRegen(PowerType power)
        {
            var powerIndex = GetPowerIndex(power);
            if (powerIndex == (uint)PowerType.Max || powerIndex >= (uint)PowerType.MaxPerClass)
                return;

            m_regenInterruptTimestamp = LoopTime.ServerTime;
            m_powerFraction[powerIndex] = 0.0f;
            SendPacket(new InterruptPowerRegen(power));
        }

        void RegenerateHealth()
        {
            uint curValue = (uint)GetHealth();
            uint maxValue = (uint)GetMaxHealth();

            if (curValue >= maxValue)
                return;

            float HealthIncreaseRate = 1.0f;

            if (GetLevel() < 15)
                HealthIncreaseRate *= (2.066f - (GetLevel() * 0.066f));

            float addValue = 0.0f;

            // polymorphed case
            if (IsPolymorphed())
                addValue = (float)GetMaxHealth() / 3;
            // normal regen case (maybe partly in combat case)
            else if (!IsInCombat() || HasAuraType(AuraType.ModRegenDuringCombat))
            {
                addValue = OCTRegenHPPerSpirit() * HealthIncreaseRate;

                if (!IsInCombat())
                {
                    addValue *= GetTotalAuraMultiplier(AuraType.ModHealthRegenPercent);
                    addValue += GetTotalAuraModifier(AuraType.ModHealthRegen) * 2 * Time.MillisecondsInSecond / (5 * Time.MillisecondsInSecond);
                }
                else if (HasAuraType(AuraType.ModRegenDuringCombat))
                    MathFunctions.ApplyPct(ref addValue, GetTotalAuraModifier(AuraType.ModRegenDuringCombat));

                if (!IsStandState())
                    addValue *= 1.5f;
            }

            // always regeneration bonus (including combat)
            addValue += GetTotalAuraModifier(AuraType.ModHealthRegenInCombat);
            addValue += m_baseHealthRegen / 2.5f;

            if (addValue < 0)
                addValue = 0;

            ModifyHealth((int)addValue);
        }

        public void ResetAllPowers()
        {
            SetFullHealth();
            switch (GetPowerType())
            {
                case PowerType.Mana:
                    SetFullPower(PowerType.Mana);
                    break;
                case PowerType.Rage:
                    SetPower(PowerType.Rage, 0);
                    break;
                case PowerType.Energy:
                    SetFullPower(PowerType.Energy);
                    break;
                case PowerType.RunicPower:
                    SetPower(PowerType.RunicPower, 0);
                    break;
                default:
                    break;
            }
        }

        public Unit GetSelectedUnit()
        {
            ObjectGuid selectionGUID = GetTarget();
            if (!selectionGUID.IsEmpty())
                return ObjAccessor.GetUnit(this, selectionGUID);
            return null;
        }

        public Player GetSelectedPlayer()
        {
            ObjectGuid selectionGUID = GetTarget();
            if (!selectionGUID.IsEmpty())
                return ObjAccessor.GetPlayer(this, selectionGUID);
            return null;
        }

        public static bool IsValidGender(Gender _gender) { return _gender <= Gender.Female; }
        public static bool IsValidClass(Class _class) { return ClassMask.Playable.HasClass(_class); }
        public static bool IsValidRace(Race _race) { return RaceMask.Playable.HasRace(_race); }

        void LeaveLFGChannel()
        {
            foreach (var i in m_channels)
            {
                if (i.IsLFG())
                {
                    i.LeaveChannel(this);
                    break;
                }
            }
        }        

        bool IsImmuneToEnvironmentalDamage()
        {
            // check for GM and death state included in isAttackableByAOE
            return (!IsTargetableForAttack(false));
        }

        public int EnvironmentalDamage(EnviromentalDamage type, int damage)
        {
            if (IsImmuneToEnvironmentalDamage())
                return 0;

            damage = (int)(damage * GetTotalAuraMultiplier(AuraType.ModEnvironmentalDamageTaken));

            // Absorb, resist some environmental damage Type
            int absorb = 0;
            int resist = 0;
            switch (type)
            {
                case EnviromentalDamage.Lava:
                case EnviromentalDamage.Slime:
                    DamageInfo dmgInfo = new(this, this, damage, null, type == EnviromentalDamage.Lava ? SpellSchoolMask.Fire : SpellSchoolMask.Nature, DamageEffectType.Direct, WeaponAttackType.BaseAttack);
                    CalcAbsorbResist(dmgInfo);
                    absorb = dmgInfo.GetAbsorb();
                    resist = dmgInfo.GetResist();
                    damage = dmgInfo.GetDamage();
                    break;
            }

            DealDamageMods(null, this, ref damage, ref absorb);

            EnvironmentalDamageLog packet = new();
            packet.Victim = GetGUID();
            packet.Type = type != EnviromentalDamage.FallToVoid ? type : EnviromentalDamage.Fall;
            packet.Amount = damage;
            packet.Absorbed = absorb;
            packet.Resisted = resist;

            int final_damage = DealDamage(this, this, damage, null, DamageEffectType.Self, SpellSchoolMask.Normal, null, false);
            packet.LogData.Initialize(this);

            SendCombatLogMessage(packet);

            if (!IsAlive())
            {
                if (type == EnviromentalDamage.Fall)                               // DealDamage not apply item durability loss at self damage
                {
                    Log.outDebug(LogFilter.Player, 
                        $"Player::EnvironmentalDamage: Player '{GetName()}' ({GetGUID()}) fall to death, " +
                        $"losing {WorldConfig.Values[WorldCfg.RateDurabilityLossOnDeath].Float} durability");

                    DurabilityLossAll(WorldConfig.Values[WorldCfg.RateDurabilityLossOnDeath].Float, false);
                    // durability lost message
                    SendDurabilityLoss(this, (int)(WorldConfig.Values[WorldCfg.RateDurabilityLossOnDeath].Float * 100.0f));
                }

                UpdateCriteria(CriteriaType.DieFromEnviromentalDamage, 1, (long)type);
            }

            return final_damage;
        }

        bool IsTotalImmune()
        {
            var immune = GetAuraEffectsByType(AuraType.SchoolImmunity);

            int immuneMask = 0;
            foreach (var eff in immune)
            {
                immuneMask |= eff.GetMiscValue();
                if (Convert.ToBoolean(immuneMask & (int)SpellSchoolMask.All))            // total immunity
                    return true;
            }
            return false;
        }

        public override bool CanNeverSee(WorldObject obj)
        {
            // the intent is to delay sending visible objects until client is ready for them
            // some gameobjects dont function correctly if they are sent
            // before TransportServerTime is correctly set (after CMSG_MOVE_INIT_ACTIVE_MOVER_COMPLETE)
            return !HasPlayerLocalFlag(PlayerLocalFlags.OverrideTransportServerTime) || base.CanNeverSee(obj);
        }

        public override bool CanAlwaysSee(WorldObject obj)
        {
            // Always can see self
            if (GetUnitBeingMoved() == obj)
                return true;

            ObjectGuid guid = m_activePlayerData.FarsightObject;
            if (!guid.IsEmpty())
            {
                if (obj.GetGUID() == guid)
                    return true;
            }

            return false;
        }

        public override bool IsAlwaysDetectableFor(WorldObject seer)
        {
            if (base.IsAlwaysDetectableFor(seer))
                return true;

            if (duel != null && duel.State != DuelState.Challenged && duel.Opponent == seer)
                return false;

            Player seerPlayer = seer.ToPlayer();
            if (seerPlayer != null)
            {
                if (IsGroupVisibleFor(seerPlayer))
                    return true;
            }

            return false;
        }

        public override bool IsNeverVisibleFor(WorldObject seer, bool allowServersideObjects = false)
        {
            if (base.IsNeverVisibleFor(seer, allowServersideObjects))
                return true;

            if (GetSession().PlayerLogout() || GetSession().PlayerLoading())
                return true;

            return false;
        }

        public void BuildPlayerRepop()
        {
            PreRessurect packet = new();
            packet.PlayerGUID = GetGUID();
            SendPacket(packet);

            // If the player has the Wisp racial then cast the Wisp aura on them
            if (HasSpell(20585))
                CastSpell(this, 20584, true);
            CastSpell(this, 8326, true);

            RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags.Release);

            // there must be SMSG.FORCE_RUN_SPEED_CHANGE, SMSG.FORCE_SWIM_SPEED_CHANGE, SMSG.MOVE_WATER_WALK
            // there must be SMSG.STOP_MIRROR_TIMER

            // the player cannot have a corpse already on current map, only bones which are not returned by GetCorpse
            WorldLocation corpseLocation = GetCorpseLocation();
            if (corpseLocation.GetMapId() == GetMapId())
            {
                Log.outError(LogFilter.Player, 
                    $"BuildPlayerRepop: player {GetName()} ({GetGUID()}) already has a corpse");
                return;
            }

            // create a corpse and place it at the player's location
            Corpse corpse = CreateCorpse();
            if (corpse == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Error creating corpse for Player {GetName()} ({GetGUID()})");
                return;
            }
            GetMap().AddToMap(corpse);

            // convert player body to ghost
            SetDeathState(DeathState.Dead);
            SetHealth(1);

            SetWaterWalking(true);
            if (!GetSession().IsLogingOut() && !HasUnitState(UnitState.Stunned))
                SetRooted(false);

            // BG - remove insignia related
            RemoveUnitFlag(UnitFlags.Skinnable);

            int corpseReclaimDelay = CalculateCorpseReclaimDelay();

            if (corpseReclaimDelay >= 0)
                SendCorpseReclaimDelay(corpseReclaimDelay);

            // to prevent cheating
            corpse.ResetGhostTime();

            StopMirrorTimers();                                     //disable timers(bars)

            // OnPlayerRepop hook
            ScriptMgr.OnPlayerRepop(this);
        }

        public void StopMirrorTimers()
        {
            StopMirrorTimer(MirrorTimerType.Fatigue);
            StopMirrorTimer(MirrorTimerType.Breath);
            StopMirrorTimer(MirrorTimerType.Fire);
        }

        public bool IsMirrorTimerActive(MirrorTimerType type)
        {
            return m_MirrorTimer[(int)type] == GetMaxTimer(type);
        }

        void HandleDrowning(Milliseconds diff)
        {
            if (m_MirrorTimerFlags == 0)
                return;

            int breathTimer = (int)MirrorTimerType.Breath;
            int fatigueTimer = (int)MirrorTimerType.Fatigue;
            int fireTimer = (int)MirrorTimerType.Fire;

            int getEnvironmentalDamage(EnviromentalDamage damageType)
            {
                byte damagePercent = 10;
                if (damageType == EnviromentalDamage.Drowning || damageType == EnviromentalDamage.Exhausted)
                    damagePercent *= 2;

                int damage = (int)(GetMaxHealth() * damagePercent / 100);

                // Randomize damage
                damage += RandomHelper.IRand(0, (int)Math.Pow(10, Math.Max(0, Math.Log10(damage) - 1)));

                return damage;
            }

            // In water
            if (m_MirrorTimerFlags.HasAnyFlag(PlayerUnderwaterState.InWater))
            {
                // Breath timer not activated - activate it
                if (m_MirrorTimer[breathTimer] == -1)
                {
                    m_MirrorTimer[breathTimer] = GetMaxTimer(MirrorTimerType.Breath);
                    SendMirrorTimer(MirrorTimerType.Breath, m_MirrorTimer[breathTimer], m_MirrorTimer[breathTimer], -1);
                }
                else                                                              // If activated - do tick
                {
                    m_MirrorTimer[breathTimer] -= diff;
                    // Timer limit - need deal damage
                    if (m_MirrorTimer[breathTimer] < 0)
                    {
                        m_MirrorTimer[breathTimer] += (Seconds)1;
                        // Calculate and deal damage
                        int damage = getEnvironmentalDamage(EnviromentalDamage.Drowning);
                        EnvironmentalDamage(EnviromentalDamage.Drowning, damage);
                    }
                    else if (!m_MirrorTimerFlagsLast.HasAnyFlag(PlayerUnderwaterState.InWater))      // Update time in client if need
                        SendMirrorTimer(MirrorTimerType.Breath, GetMaxTimer(MirrorTimerType.Breath), m_MirrorTimer[breathTimer], -1);
                }
            }
            else if (m_MirrorTimer[breathTimer] != -1)        // Regen timer
            {
                int regen = 10;
                Milliseconds UnderWaterTime = GetMaxTimer(MirrorTimerType.Breath);
                // Need breath regen
                m_MirrorTimer[breathTimer] += (Milliseconds)(regen * diff);
                if (m_MirrorTimer[breathTimer] >= UnderWaterTime || !IsAlive())
                    StopMirrorTimer(MirrorTimerType.Breath);
                else if (m_MirrorTimerFlagsLast.HasAnyFlag(PlayerUnderwaterState.InWater))
                    SendMirrorTimer(MirrorTimerType.Breath, UnderWaterTime, m_MirrorTimer[breathTimer], regen);
            }

            // In dark water
            if (m_MirrorTimerFlags.HasAnyFlag(PlayerUnderwaterState.InDarkWater))
            {
                // Fatigue timer not activated - activate it
                if (m_MirrorTimer[fatigueTimer] == -1)
                {
                    m_MirrorTimer[fatigueTimer] = GetMaxTimer(MirrorTimerType.Fatigue);
                    SendMirrorTimer(MirrorTimerType.Fatigue, m_MirrorTimer[fatigueTimer], m_MirrorTimer[fatigueTimer], -1);
                }
                else
                {
                    m_MirrorTimer[fatigueTimer] -= diff;
                    // Timer limit - need deal damage or teleport ghost to graveyard
                    if (m_MirrorTimer[fatigueTimer] < 0)
                    {
                        m_MirrorTimer[fatigueTimer] += (Seconds)1;
                        if (IsAlive())                                            // Calculate and deal damage
                        {
                            int damage = getEnvironmentalDamage(EnviromentalDamage.Exhausted);
                            EnvironmentalDamage(EnviromentalDamage.Exhausted, damage);
                        }
                        else if (HasPlayerFlag(PlayerFlags.Ghost))       // Teleport ghost to graveyard
                            RepopAtGraveyard();
                    }
                    else if (!m_MirrorTimerFlagsLast.HasAnyFlag(PlayerUnderwaterState.InDarkWater))
                        SendMirrorTimer(MirrorTimerType.Fatigue, GetMaxTimer(MirrorTimerType.Fatigue), m_MirrorTimer[fatigueTimer], -1);
                }
            }
            else if (m_MirrorTimer[fatigueTimer] != -1)       // Regen timer
            {
                int regen = 10;
                Milliseconds DarkWaterTime = GetMaxTimer(MirrorTimerType.Fatigue);
                m_MirrorTimer[fatigueTimer] += (Milliseconds)(regen * diff);
                if (m_MirrorTimer[fatigueTimer] >= DarkWaterTime || !IsAlive())
                    StopMirrorTimer(MirrorTimerType.Fatigue);
                else if (m_MirrorTimerFlagsLast.HasAnyFlag(PlayerUnderwaterState.InDarkWater))
                    SendMirrorTimer(MirrorTimerType.Fatigue, DarkWaterTime, m_MirrorTimer[fatigueTimer], regen);
            }

            if (m_MirrorTimerFlags.HasAnyFlag(PlayerUnderwaterState.InLava) && !(_lastLiquid != null && _lastLiquid.SpellID != 0))
            {
                // Breath timer not activated - activate it
                if (m_MirrorTimer[fireTimer] == -1)
                    m_MirrorTimer[fireTimer] = GetMaxTimer(MirrorTimerType.Fire);
                else
                {
                    m_MirrorTimer[fireTimer] -= diff;
                    if (m_MirrorTimer[fireTimer] < 0)
                    {
                        m_MirrorTimer[fireTimer] += (Seconds)1;
                        // Calculate and deal damage
                        int damage = getEnvironmentalDamage(EnviromentalDamage.Lava);
                        if (m_MirrorTimerFlags.HasAnyFlag(PlayerUnderwaterState.InLava))
                            EnvironmentalDamage(EnviromentalDamage.Lava, damage);
                        // need to skip Slime damage in Undercity,
                        // maybe someone can find better way to handle environmental damage
                        //else if (m_zoneUpdateId != 1497)
                        //    EnvironmentalDamage(DAMAGE_SLIME, damage);
                    }
                }
            }
            else
                m_MirrorTimer[fireTimer] = new(-1);

            // Recheck timers flag
            m_MirrorTimerFlags &= ~PlayerUnderwaterState.ExistTimers;
            for (byte i = 0; i < (int)MirrorTimerType.Max; ++i)
            {
                if (m_MirrorTimer[i] != -1)
                {
                    m_MirrorTimerFlags |= PlayerUnderwaterState.ExistTimers;
                    break;
                }
            }
            m_MirrorTimerFlagsLast = m_MirrorTimerFlags;
        }

        void HandleSobering()
        {
            m_drunkTimer = Milliseconds.Zero;

            byte currentDrunkValue = GetDrunkValue();
            byte drunk = (byte)(currentDrunkValue != 0 ? --currentDrunkValue : 0);
            SetDrunkValue(drunk);
        }

        void SendMirrorTimer(MirrorTimerType Type, Milliseconds MaxValue, Milliseconds CurrentValue, int Regen)
        {
            if (MaxValue == -1)
            {
                if (CurrentValue != -1)
                    StopMirrorTimer(Type);
                return;
            }

            SendPacket(new StartMirrorTimer(Type, CurrentValue, MaxValue, Regen, 0, false));
        }

        void StopMirrorTimer(MirrorTimerType Type)
        {
            m_MirrorTimer[(int)Type] = new(-1);
            SendPacket(new StopMirrorTimer(Type));
        }

        Milliseconds GetMaxTimer(MirrorTimerType timer)
        {
            switch (timer)
            {
                case MirrorTimerType.Fatigue:
                    return (Minutes)1;
                case MirrorTimerType.Breath:
                {
                    if (!IsAlive() || HasAuraType(AuraType.WaterBreathing)
                        || GetSession().GetSecurity() >= (AccountTypes)WorldConfig.Values[WorldCfg.DisableBreathing].Int32)
                    {
                        return new(-1);
                    }

                    Milliseconds UnderWaterTime = (Minutes)3;
                    UnderWaterTime = (Milliseconds)(UnderWaterTime * GetTotalAuraMultiplier(AuraType.ModWaterBreathing));
                    return UnderWaterTime;
                }
                case MirrorTimerType.Fire:
                {
                    if (!IsAlive())
                        return new(-1);

                    return (Seconds)1;
                }
                default:
                    return Milliseconds.Zero;
            }
        }

        public void UpdateMirrorTimers()
        {
            // Desync flags for update on next HandleDrowning
            if (m_MirrorTimerFlags != 0)
                m_MirrorTimerFlagsLast = ~m_MirrorTimerFlags;
        }

        public void ResurrectPlayer(float restore_percent, bool applySickness = false)
        {
            SetAreaSpiritHealer(null);

            DeathReleaseLoc packet = new();
            packet.MapID = -1;
            SendPacket(packet);

            // speed change, land walk

            // remove death flag + set aura
            RemovePlayerFlag(PlayerFlags.IsOutOfBounds);

            // This must be called always even on Players with race != RACE_NIGHTELF in case of faction change
            RemoveAurasDueToSpell(20584);                       // speed bonuses
            RemoveAurasDueToSpell(8326);                            // SPELL_AURA_GHOST

            if (GetSession().IsARecruiter() || (GetSession().GetRecruiterId() != 0))
                SetDynamicFlag(UnitDynFlags.ReferAFriend);

            SetDeathState(DeathState.Alive);

            // add the flag to make sure opcode is always sent
            AddUnitMovementFlag(MovementFlag.WaterWalk);
            SetWaterWalking(false);
            if (!HasUnitState(UnitState.Stunned))
                SetRooted(false);

            m_deathTimer = Milliseconds.Zero;

            // set health/powers (0- will be set in caller)
            if (restore_percent > 0.0f)
            {
                SetHealth((long)(GetMaxHealth() * restore_percent));
                SetPower(PowerType.Mana, (int)(GetMaxPower(PowerType.Mana) * restore_percent));
                SetPower(PowerType.Rage, 0);
                SetPower(PowerType.Energy, (int)(GetMaxPower(PowerType.Energy) * restore_percent));
                SetPower(PowerType.Focus, (int)(GetMaxPower(PowerType.Focus) * restore_percent));
            }

            // trigger update zone for alive state zone updates
            int newzone, newarea;
            GetZoneAndAreaId(out newzone, out newarea);
            UpdateZone(newzone, newarea);
            OutdoorPvPMgr.HandlePlayerResurrects(this, newzone);

            if (InBattleground())
            {
                Battleground bg = GetBattleground();
                if (bg != null)
                    bg.HandlePlayerResurrect(this);
            }

            // update visibility
            UpdateObjectVisibility();

            // recast lost by death auras of any items held in the inventory
            CastAllObtainSpells();

            if (!applySickness)
                return;

            //Characters from level 1-10 are not affected by resurrection sickness.
            //Characters from level 11-19 will suffer from one minute of sickness
            //for each level they are above 10.
            //Characters level 20 and up suffer from ten minutes of sickness.
            int startLevel = WorldConfig.Values[WorldCfg.DeathSicknessLevel].Int32;
            var raceEntry = CliDB.ChrRacesStorage.LookupByKey((int)GetRace());

            if (GetLevel() >= startLevel)
            {
                // set resurrection sickness
                CastSpell(this, raceEntry.ResSicknessSpellID, true);

                // not full duration
                if (GetLevel() < startLevel + 9)
                {
                    Seconds delta = (Seconds)(GetLevel() - startLevel + 1) * Time.Minute;
                    Aura aur = GetAura(raceEntry.ResSicknessSpellID, GetGUID());
                    if (aur != null)
                        aur.SetDuration(delta);

                }
            }
        }

        ObjectGuid GetSpiritHealerGUID() { return _areaSpiritHealerGUID; }

        public bool CanAcceptAreaSpiritHealFrom(Unit spiritHealer) { return spiritHealer.GetGUID() == _areaSpiritHealerGUID; }

        public void SetAreaSpiritHealer(Creature creature)
        {
            if (creature == null)
            {
                _areaSpiritHealerGUID = ObjectGuid.Empty;
                RemoveAurasDueToSpell(BattlegroundConst.SpellWaitingForResurrect);
                return;
            }

            if (!creature.IsAreaSpiritHealer())
                return;

            _areaSpiritHealerGUID = creature.GetGUID();
            CastSpell(null, BattlegroundConst.SpellWaitingForResurrect);
        }

        public void SendAreaSpiritHealerTime(Unit spiritHealer)
        {
            int timeLeft = 0;
            Spell spell = spiritHealer.GetCurrentSpell(CurrentSpellTypes.Channeled);
            if (spell != null)
                timeLeft = spell.GetTimer();

            SendAreaSpiritHealerTime(spiritHealer.GetGUID(), timeLeft);
        }

        public void SendAreaSpiritHealerTime(ObjectGuid spiritHealerGUID, int timeLeft)
        {
            AreaSpiritHealerTime areaSpiritHealerTime = new();
            areaSpiritHealerTime.HealerGuid = spiritHealerGUID;
            areaSpiritHealerTime.TimeLeft = timeLeft;
            SendPacket(areaSpiritHealerTime);
        }

        public void KillPlayer()
        {
            if (IsFlying() && GetTransport() == null)
                GetMotionMaster().MoveFall();

            SetRooted(true);

            StopMirrorTimers();                                     //disable timers(bars)

            SetDeathState(DeathState.Corpse);

            ReplaceAllDynamicFlags(UnitDynFlags.None);
            if (!CliDB.MapStorage.LookupByKey(GetMapId()).Instanceable && !HasAuraType(AuraType.PreventResurrection))
                SetPlayerLocalFlag(PlayerLocalFlags.ReleaseTimer);
            else
                RemovePlayerLocalFlag(PlayerLocalFlags.ReleaseTimer);

            // 6 minutes until repop at graveyard
            m_deathTimer = (Minutes)6;

            UpdateCorpseReclaimDelay();                             // dependent at use SetDeathPvP() call before kill

            int corpseReclaimDelay = CalculateCorpseReclaimDelay();

            if (corpseReclaimDelay >= 0)
                SendCorpseReclaimDelay(corpseReclaimDelay);

            // don't create corpse at this moment, player might be falling

            // update visibility
            UpdateObjectVisibility();
        }

        public static void OfflineResurrect(ObjectGuid guid, SQLTransaction trans)
        {
            Corpse.DeleteFromDB(guid, trans);
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
            stmt.SetUInt16(0, (ushort)AtLoginFlags.Resurrect);
            stmt.SetInt64(1, guid.GetCounter());
            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        Corpse CreateCorpse()
        {
            // prevent existence 2 corpse for player
            SpawnCorpseBones();

            Corpse corpse = new(Convert.ToBoolean(m_ExtraFlags & PlayerExtraFlags.PVPDeath) ? CorpseType.ResurrectablePVP : CorpseType.ResurrectablePVE);
            SetPvPDeath(false);

            if (!corpse.Create(GetMap().GenerateLowGuid(HighGuid.Corpse), this))
                return null;

            _corpseLocation = new WorldLocation(this);

            CorpseFlags flags = 0;
            if (HasPvpFlag(UnitPVPStateFlags.PvP))
                flags |= CorpseFlags.PvP;
            if (InBattleground() && !InArena())
                flags |= CorpseFlags.Skinnable;                      // to be able to remove insignia
            if (HasPvpFlag(UnitPVPStateFlags.FFAPvp))
                flags |= CorpseFlags.FFAPvP;

            corpse.SetRace(GetRace());
            corpse.SetSex(GetNativeGender());
            corpse.SetClass(GetClass());
            corpse.SetCustomizations(m_playerData.Customizations.GetValues());
            corpse.ReplaceAllFlags(flags);
            corpse.SetDisplayId(GetNativeDisplayId());
            corpse.SetFactionTemplate(CliDB.ChrRacesStorage.LookupByKey((int)GetRace()).FactionID);

            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; i++)
            {
                if (m_items[i] != null)
                {
                    var itemDisplayId = m_items[i].GetDisplayId(this);
                    InventoryType itemInventoryType;
                    ItemRecord itemEntry = CliDB.ItemStorage.LookupByKey(m_items[i].GetVisibleEntry(this));
                    if (itemEntry != null)
                        itemInventoryType = itemEntry.InventoryType;
                    else
                        itemInventoryType = m_items[i].GetTemplate().GetInventoryType();

                    corpse.SetItem(i, itemDisplayId | ((int)itemInventoryType << 24));
                }
            }

            // register for player, but not show
            GetMap().AddCorpse(corpse);

            corpse.UpdatePositionData();
            corpse.SetZoneScript();

            // we do not need to save corpses for instances
            if (!GetMap().Instanceable())
                corpse.SaveToDB();

            return corpse;
        }

        public void SpawnCorpseBones(bool triggerSave = true)
        {
            _corpseLocation = new WorldLocation();
            if (GetMap().ConvertCorpseToBones(GetGUID()) != null)
            {
                if (triggerSave && !GetSession().PlayerLogoutWithSave())   // at logout we will already store the player
                    SaveToDB();                                             // prevent loading as ghost without corpse
            }
        }

        public Corpse GetCorpse() { return GetMap().GetCorpseByPlayer(GetGUID()); }

        public void RepopAtGraveyard()
        {
            // note: this can be called also when the player is alive
            // for example from WorldSession.HandleMovementOpcodes

            AreaTableRecord zone = CliDB.AreaTableStorage.LookupByKey(GetAreaId());

            bool shouldResurrect = false;
            // Such zones are considered unreachable as a ghost and the player must be automatically revived
            if ((!IsAlive() && zone != null && zone.HasFlag(AreaFlags.NoGhostOnRelease)) || GetMap().IsNonRaidDungeon() || GetMap().IsRaid() || GetTransport() != null || GetPositionZ() < GetMap().GetMinHeight(GetPhaseShift(), GetPositionX(), GetPositionY()))
            {
                shouldResurrect = true;
                SpawnCorpseBones();
            }

            WorldSafeLocsEntry ClosestGrave = null;

            // Special handle for Battlegroundmaps
            Battleground bg = GetBattleground();
            if (bg != null)
                ClosestGrave = bg.GetClosestGraveyard(this);
            else
            {
                BattleField bf = BattleFieldMgr.GetBattlefieldToZoneId(GetMap(), GetZoneId());
                if (bf != null)
                    ClosestGrave = bf.GetClosestGraveyard(this);
                else
                    ClosestGrave = ObjectMgr.GetClosestGraveyard(this, GetTeam(), this);
            }

            // stop countdown until repop
            m_deathTimer = Milliseconds.Zero;

            // if no grave found, stay at the current location
            // and don't show spirit healer location
            if (ClosestGrave != null)
            {
                TeleportTo(ClosestGrave.Loc, shouldResurrect ? TeleportToOptions.ReviveAtTeleport : 0);
                if (IsDead())                                        // not send if alive, because it used in TeleportTo()
                {
                    DeathReleaseLoc packet = new();
                    packet.MapID = ClosestGrave.Loc.GetMapId();
                    packet.Loc = ClosestGrave.Loc;
                    SendPacket(packet);
                }
            }
            else if (GetPositionZ() < GetMap().GetMinHeight(GetPhaseShift(), GetPositionX(), GetPositionY()))
                TeleportTo(homebind);

            RemovePlayerFlag(PlayerFlags.IsOutOfBounds);
        }

        public bool HasCorpse()
        {
            return _corpseLocation != null && _corpseLocation.GetMapId() != -1;
        }

        public WorldLocation GetCorpseLocation() { return _corpseLocation; }

        public Seconds GetCorpseReclaimDelay(bool pvp)
        {
            if (pvp)
            {
                if (!WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPvp].Bool)
                    return PlayerConst.copseReclaimDelay[0];
            }
            else if (!WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPve].Bool)
                return Seconds.Zero;

            ServerTime now = LoopTime.ServerTime;
            // 0..2 full period
            // should be ceil(x)-1 but not floor(x)
            long count = (long)((now < m_deathExpireTime - (Seconds)1) ? (m_deathExpireTime - (Seconds)1 - now) / PlayerConst.DeathExpireStep : 0);
            return PlayerConst.copseReclaimDelay[count];
        }

        void UpdateCorpseReclaimDelay()
        {
            bool pvp = m_ExtraFlags.HasAnyFlag(PlayerExtraFlags.PVPDeath);

            if ((pvp && !WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPvp].Bool) ||
                (!pvp && !WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPve].Bool))
                return;

            ServerTime now = LoopTime.ServerTime;
            if (now < m_deathExpireTime)
            {
                // full and partly periods 1..3
                long count = (long)((m_deathExpireTime - now) / PlayerConst.DeathExpireStep) + 1;
                if (count < PlayerConst.MaxDeathCount)
                    m_deathExpireTime = now + (count + 1) * PlayerConst.DeathExpireStep;
                else
                    m_deathExpireTime = now + PlayerConst.MaxDeathCount * PlayerConst.DeathExpireStep;
            }
            else
                m_deathExpireTime = now + PlayerConst.DeathExpireStep;
        }

        Milliseconds CalculateCorpseReclaimDelay(bool load = false)
        {
            Corpse corpse = GetCorpse();
            if (load && corpse == null)
                return new(-1);

            bool pvp = corpse != null ? corpse.GetCorpseType() == CorpseType.ResurrectablePVP : (m_ExtraFlags & PlayerExtraFlags.PVPDeath) != 0;

            Seconds delay;
            if (load)
            {
                if (corpse.GetGhostTime() > m_deathExpireTime)
                    return new(-1);

                long count = 0;
                if ((pvp && WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPvp].Bool) ||
                   (!pvp && WorldConfig.Values[WorldCfg.DeathCorpseReclaimDelayPve].Bool))
                {
                    count = (long)((m_deathExpireTime - corpse.GetGhostTime()) / PlayerConst.DeathExpireStep);

                    if (count >= PlayerConst.MaxDeathCount)
                        count = PlayerConst.MaxDeathCount - 1;
                }

                ServerTime expected_time = corpse.GetGhostTime() + PlayerConst.copseReclaimDelay[count];
                ServerTime now = LoopTime.ServerTime;

                if (now >= expected_time)
                    return new(-1);

                delay = (Seconds)(expected_time - now);
            }
            else
                delay = GetCorpseReclaimDelay(pvp);

            return delay;
        }

        void SendCorpseReclaimDelay(int delay)
        {
            CorpseReclaimDelay packet = new();
            packet.Remaining = (uint)delay;
            SendPacket(packet);
        }

        public override bool CanFly() { return m_movementInfo.HasMovementFlag(MovementFlag.CanFly); }
        public override bool CanEnterWater() { return true; }

        public Pet GetPet()
        {
            ObjectGuid petGuid = GetPetGUID();
            if (!petGuid.IsEmpty())
            {
                if (!petGuid.IsPet())
                    return null;

                Pet pet = ObjectAccessor.GetPet(this, petGuid);
                if (pet == null)
                    return null;

                if (IsInWorld)
                    return pet;
            }

            return null;
        }

        public Pet SummonPet(int entry, PetSaveMode? slot, float x, float y, float z, float ang, TimeSpan duration)
        {
            return SummonPet(entry, slot, x, y, z, ang, duration, out _);
        }

        public Pet SummonPet(int entry, PetSaveMode? slot, float x, float y, float z, float ang, TimeSpan duration, out bool isNew)
        {
            isNew = false;

            PetStable petStable = GetOrInitPetStable();

            Pet pet = new(this, PetType.Summon);
            if (pet.LoadPetFromDB(this, entry, 0, false, slot))
            {
                if (duration > TimeSpan.Zero)
                    pet.SetDuration(duration);

                return pet;
            }

            // petentry == 0 for hunter "call pet" (current pet summoned if any)
            if (entry == 0)
            {
                pet.Dispose();
                return null;
            }

            // only SUMMON_PET are handled here

            pet.Relocate(x, y, z, ang);
            if (!pet.IsPositionValid())
            {
                Log.outError(LogFilter.Server, 
                    $"Pet (guidlow {pet.GetGUID()}, entry {pet.GetEntry()}) not summoned. " +
                    $"Suggested coordinates isn't valid (X: {pet.GetPositionX()} Y: {pet.GetPositionY()})");

                pet.Dispose();
                return null;
            }

            Map map = GetMap();
            var petNumber = ObjectMgr.GeneratePetNumber();
            if (!pet.Create(map.GenerateLowGuid(HighGuid.Pet), map, entry, petNumber))
            {
                Log.outError(LogFilter.Server, $"no such creature entry {entry}");
                pet.Dispose();
                return null;
            }

            if (petStable.GetCurrentPet() != null)
                RemovePet(null, PetSaveMode.NotInSlot);

            PhasingHandler.InheritPhaseShift(pet, this);

            pet.SetCreatorGUID(GetGUID());
            pet.SetFaction(GetFaction());
            pet.ReplaceAllNpcFlags(NPCFlags1.None);
            pet.ReplaceAllNpcFlags2(NPCFlags2.None);
            pet.InitStatsForLevel(GetLevel());

            SetMinion(pet, true);

            // this enables pet details window (Shift+P)
            pet.GetCharmInfo().SetPetNumber(petNumber, true);
            pet.SetClass(Class.Mage);
            pet.SetPetExperience(0);
            pet.SetPetNextLevelExperience(1000);
            pet.SetFullHealth();
            pet.SetFullPower(PowerType.Mana);
            pet.SetPetNameTimestamp(LoopTime.ServerTime);

            map.AddToMap(pet.ToCreature());

            Cypher.Assert(!petStable.CurrentPetIndex.HasValue);
            petStable.SetCurrentUnslottedPetIndex(petStable.UnslottedPets.Count);
            PetStable.PetInfo petInfo = new();
            pet.FillPetInfo(petInfo);
            petStable.UnslottedPets.Add(petInfo);

            pet.InitPetCreateSpells();
            pet.SavePetToDB(PetSaveMode.AsCurrent);
            PetSpellInitialize();

            if (duration > TimeSpan.Zero)
                pet.SetDuration(duration);

            //ObjectAccessor.UpdateObjectVisibility(pet);

            isNew = true;

            return pet;
        }

        public void RemovePet(Pet pet, PetSaveMode mode, bool returnreagent = false)
        {
            if (pet == null)
                pet = GetPet();

            if (pet != null)
            {
                Log.outDebug(LogFilter.Pet, $"RemovePet {pet.GetEntry()}, {mode}, {returnreagent}.");

                if (pet.m_removed)
                    return;
            }

            if (returnreagent && (pet != null || m_temporaryUnsummonedPetNumber != 0) && !InBattleground())
            {
                //returning of reagents only for players, so best done here
                var spellId = pet != null ? pet.m_unitData.CreatedBySpell : m_oldpetspell;
                SpellInfo spellInfo = SpellMgr.GetSpellInfo(spellId, GetMap().GetDifficultyID());

                if (spellInfo != null)
                {
                    for (int i = 0; i < SpellConst.MaxReagents; ++i)
                    {
                        if (spellInfo.Reagent[i] > 0)
                        {
                            //for succubus, voidwalker, felhunter and felguard credit soulshard when despawn reason other than death (out of range, logout)
                            InventoryResult msg = CanStoreNewItem(ItemPos.Undefined, out var dest, spellInfo.Reagent[i], spellInfo.ReagentCount[i]);
                            if (msg == InventoryResult.Ok)
                            {
                                Item item = StoreNewItem(dest, spellInfo.Reagent[i], true);
                                if (IsInWorld)
                                    SendNewItem(item, spellInfo.ReagentCount[i], true, false);
                            }
                        }
                    }
                }
                m_temporaryUnsummonedPetNumber = 0;
            }

            if (pet == null)
            {
                // Handle removing pet while it is in "temporarily unsummoned" state, for example on mount
                if (mode == PetSaveMode.NotInSlot && m_petStable != null && m_petStable.CurrentPetIndex.HasValue)
                    m_petStable.CurrentPetIndex = null;

                return;
            }

            pet.CombatStop();

            // only if current pet in slot
            pet.SavePetToDB(mode);

            PetStable.PetInfo currentPet = m_petStable.GetCurrentPet();
            Cypher.Assert(currentPet != null && currentPet.PetNumber == pet.GetCharmInfo().GetPetNumber());
            if (mode == PetSaveMode.NotInSlot)
                m_petStable.CurrentPetIndex = null;
            else if (mode == PetSaveMode.AsDeleted)
            {
                if (m_activePlayerData.PetStable.HasValue())
                {
                    int ufIndex = m_activePlayerData.PetStable.GetValue().Pets.FindIndexIf(p => p.PetNumber == currentPet.PetNumber);
                    if (ufIndex >= 0)
                    {
                        StableInfo stableInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.PetStable);
                        RemoveDynamicUpdateFieldValue(stableInfo.ModifyValue(stableInfo.Pets), ufIndex);
                    }
                }

                var petIndex = m_petStable.GetCurrentActivePetIndex();
                if (petIndex.HasValue)
                    m_petStable.ActivePets[petIndex.Value] = null;

                m_petStable.CurrentPetIndex = null;
            }
            // else if (stable slots) handled in opcode handlers due to required swaps
            // else (current pet) doesnt need to do anything

            SetMinion(pet, false);

            pet.AddObjectToRemoveList();
            pet.m_removed = true;

            if (pet.IsControlled())
            {
                SendPacket(new PetSpells());

                if (GetGroup() != null)
                    SetGroupUpdateFlag(GroupUpdateFlags.Pet);
            }
        }

        public void SendTameFailure(PetTameResult result)
        {
            PetTameFailure petTameFailure = new();
            petTameFailure.Result = (byte)result;
            SendPacket(petTameFailure);
        }

        public void AddPetAura(PetAura petSpell)
        {
            m_petAuras.Add(petSpell);

            Pet pet = GetPet();
            if (pet != null)
                pet.CastPetAura(petSpell);
        }

        public void RemovePetAura(PetAura petSpell)
        {
            m_petAuras.Remove(petSpell);

            Pet pet = GetPet();
            if (pet != null)
                pet.RemoveAurasDueToSpell(petSpell.GetAura(pet.GetEntry()));
        }

        public bool InArena()
        {
            Battleground bg = GetBattleground();
            if (bg == null || !bg.IsArena())
                return false;

            return true;
        }

        public void SendOnCancelExpectedVehicleRideAura()
        {
            SendPacket(new OnCancelExpectedRideVehicleAura());
        }

        public void SendMovementSetCollisionHeight(float height, UpdateCollisionHeightReason reason)
        {
            MoveSetCollisionHeight setCollisionHeight = new();
            setCollisionHeight.MoverGUID = GetGUID();
            setCollisionHeight.SequenceIndex = m_movementCounter++;
            setCollisionHeight.Height = height;
            setCollisionHeight.Scale = GetObjectScale();
            setCollisionHeight.MountDisplayID = GetMountDisplayId();
            setCollisionHeight.ScaleDuration = m_unitData.ScaleDuration;
            setCollisionHeight.Reason = reason;
            SendPacket(setCollisionHeight);

            MoveUpdateCollisionHeight updateCollisionHeight = new();
            updateCollisionHeight.Status = m_movementInfo;
            updateCollisionHeight.Height = height;
            updateCollisionHeight.Scale = GetObjectScale();
            SendMessageToSet(updateCollisionHeight, false);
        }

        public void SendPlayerChoice(ObjectGuid sender, int choiceId)
        {
            PlayerChoice playerChoice = ObjectMgr.GetPlayerChoice(choiceId);
            if (playerChoice == null)
                return;

            Locale locale = GetSession().GetSessionDbLocaleIndex();
            PlayerChoiceLocale playerChoiceLocale = locale != Locale.enUS ? ObjectMgr.GetPlayerChoiceLocale(choiceId) : null;

            PlayerTalkClass.GetInteractionData().Reset();
            PlayerTalkClass.GetInteractionData().SourceGuid = sender;
            PlayerTalkClass.GetInteractionData().PlayerChoiceId = choiceId;

            DisplayPlayerChoice displayPlayerChoice = new();
            displayPlayerChoice.SenderGUID = sender;
            displayPlayerChoice.ChoiceID = choiceId;
            displayPlayerChoice.UiTextureKitID = playerChoice.UiTextureKitId;
            displayPlayerChoice.SoundKitID = playerChoice.SoundKitId;
            displayPlayerChoice.Question = playerChoice.Question;
            if (playerChoiceLocale != null)
                ObjectManager.GetLocaleString(playerChoiceLocale.Question, locale, ref displayPlayerChoice.Question);

            displayPlayerChoice.CloseChoiceFrame = false;
            displayPlayerChoice.HideWarboardHeader = playerChoice.HideWarboardHeader;
            displayPlayerChoice.KeepOpenAfterChoice = playerChoice.KeepOpenAfterChoice;

            for (var i = 0; i < playerChoice.Responses.Count; ++i)
            {
                PlayerChoiceResponse playerChoiceResponseTemplate = playerChoice.Responses[i];
                var playerChoiceResponse = new Networking.Packets.PlayerChoiceResponse();

                playerChoiceResponse.ResponseID = playerChoiceResponseTemplate.ResponseId;
                playerChoiceResponse.ResponseIdentifier = playerChoiceResponseTemplate.ResponseIdentifier;
                playerChoiceResponse.ChoiceArtFileID = playerChoiceResponseTemplate.ChoiceArtFileId;
                playerChoiceResponse.Flags = playerChoiceResponseTemplate.Flags;
                playerChoiceResponse.WidgetSetID = playerChoiceResponseTemplate.WidgetSetID;
                playerChoiceResponse.UiTextureAtlasElementID = playerChoiceResponseTemplate.UiTextureAtlasElementID;
                playerChoiceResponse.SoundKitID = playerChoiceResponseTemplate.SoundKitID;
                playerChoiceResponse.GroupID = playerChoiceResponseTemplate.GroupID;
                playerChoiceResponse.UiTextureKitID = playerChoiceResponseTemplate.UiTextureKitID;
                playerChoiceResponse.Answer = playerChoiceResponseTemplate.Answer;
                playerChoiceResponse.Header = playerChoiceResponseTemplate.Header;
                playerChoiceResponse.SubHeader = playerChoiceResponseTemplate.SubHeader;
                playerChoiceResponse.ButtonTooltip = playerChoiceResponseTemplate.ButtonTooltip;
                playerChoiceResponse.Description = playerChoiceResponseTemplate.Description;
                playerChoiceResponse.Confirmation = playerChoiceResponseTemplate.Confirmation;
                if (playerChoiceLocale != null)
                {
                    PlayerChoiceResponseLocale playerChoiceResponseLocale = playerChoiceLocale.Responses.LookupByKey(playerChoiceResponseTemplate.ResponseId);
                    if (playerChoiceResponseLocale != null)
                    {
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.Answer, locale, ref playerChoiceResponse.Answer);
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.Header, locale, ref playerChoiceResponse.Header);
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.SubHeader, locale, ref playerChoiceResponse.SubHeader);
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.ButtonTooltip, locale, ref playerChoiceResponse.ButtonTooltip);
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.Description, locale, ref playerChoiceResponse.Description);
                        ObjectManager.GetLocaleString(playerChoiceResponseLocale.Confirmation, locale, ref playerChoiceResponse.Confirmation);
                    }
                }

                if (playerChoiceResponseTemplate.Reward != null)
                {
                    var reward = new Networking.Packets.PlayerChoiceResponseReward();
                    reward.TitleID = playerChoiceResponseTemplate.Reward.TitleId;
                    reward.PackageID = playerChoiceResponseTemplate.Reward.PackageId;
                    reward.SkillLineID = playerChoiceResponseTemplate.Reward.SkillLineId;
                    reward.SkillPointCount = playerChoiceResponseTemplate.Reward.SkillPointCount;
                    reward.ArenaPointCount = playerChoiceResponseTemplate.Reward.ArenaPointCount;
                    reward.HonorPointCount = playerChoiceResponseTemplate.Reward.HonorPointCount;
                    reward.Money = playerChoiceResponseTemplate.Reward.Money;
                    reward.Xp = playerChoiceResponseTemplate.Reward.Xp;

                    foreach (var item in playerChoiceResponseTemplate.Reward.Items)
                    {
                        var rewardEntry = new Networking.Packets.PlayerChoiceResponseRewardEntry();
                        rewardEntry.Item.ItemID = item.Id;
                        rewardEntry.Quantity = item.Quantity;
                        if (!item.BonusListIDs.Empty())
                        {
                            rewardEntry.Item.ItemBonus = new();
                            rewardEntry.Item.ItemBonus.BonusListIDs = item.BonusListIDs;
                        }
                        reward.Items.Add(rewardEntry);
                    }

                    foreach (var currency in playerChoiceResponseTemplate.Reward.Currency)
                    {
                        var rewardEntry = new Networking.Packets.PlayerChoiceResponseRewardEntry();
                        rewardEntry.Item.ItemID = currency.Id;
                        rewardEntry.Quantity = currency.Quantity;
                        reward.Items.Add(rewardEntry);
                    }

                    foreach (var faction in playerChoiceResponseTemplate.Reward.Faction)
                    {
                        var rewardEntry = new Networking.Packets.PlayerChoiceResponseRewardEntry();
                        rewardEntry.Item.ItemID = faction.Id;
                        rewardEntry.Quantity = faction.Quantity;
                        reward.Items.Add(rewardEntry);
                    }

                    foreach (PlayerChoiceResponseRewardItem item in playerChoiceResponseTemplate.Reward.ItemChoices)
                    {
                        var rewardEntry = new Networking.Packets.PlayerChoiceResponseRewardEntry();
                        rewardEntry.Item.ItemID = item.Id;
                        rewardEntry.Quantity = item.Quantity;
                        if (!item.BonusListIDs.Empty())
                        {
                            rewardEntry.Item.ItemBonus = new();
                            rewardEntry.Item.ItemBonus.BonusListIDs = item.BonusListIDs;
                        }

                        reward.ItemChoices.Add(rewardEntry);
                    }

                    playerChoiceResponse.Reward = reward;
                    displayPlayerChoice.Responses[i] = playerChoiceResponse;
                }

                playerChoiceResponse.RewardQuestID = playerChoiceResponseTemplate.RewardQuestID;

                if (playerChoiceResponseTemplate.MawPower.HasValue)
                {
                    var mawPower = new Networking.Packets.PlayerChoiceResponseMawPower();
                    mawPower.TypeArtFileID = playerChoiceResponse.MawPower.Value.TypeArtFileID;
                    mawPower.Rarity = playerChoiceResponse.MawPower.Value.Rarity;
                    mawPower.RarityColor = playerChoiceResponse.MawPower.Value.RarityColor;
                    mawPower.SpellID = playerChoiceResponse.MawPower.Value.SpellID;
                    mawPower.MaxStacks = playerChoiceResponse.MawPower.Value.MaxStacks;

                    playerChoiceResponse.MawPower = mawPower;
                }
            }

            SendPacket(displayPlayerChoice);
        }

        public bool MeetPlayerCondition(int conditionId)
        {
            PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(conditionId);
            if (playerCondition != null)
            {
                if (!ConditionManager.IsPlayerMeetingCondition(this, playerCondition))
                    return false;
            }

            return true;
        }

        bool IsInFriendlyArea()
        {
            var areaEntry = CliDB.AreaTableStorage.LookupByKey(GetAreaId());
            if (areaEntry != null)
                return IsFriendlyArea(areaEntry);

            return false;
        }

        bool IsFriendlyArea(AreaTableRecord areaEntry)
        {
            Cypher.Assert(areaEntry != null);

            var factionTemplate = GetFactionTemplateEntry();
            if (factionTemplate == null)
                return false;

            if ((factionTemplate.FriendGroup & areaEntry.FactionGroupMask) == 0)
                return false;

            return true;
        }

        public void SetWarModeDesired(bool enabled)
        {
            // Only allow to toggle on when in stormwind/orgrimmar, and to toggle off in any rested place.
            // Also disallow when in combat
            if ((enabled == IsWarModeDesired()) || IsInCombat() || !HasPlayerFlag(PlayerFlags.Resting))
                return;

            if (enabled && !CanEnableWarModeInArea())
                return;

            // Don't allow to chang when aura SPELL_PVP_RULES_ENABLED is on
            if (HasAura(PlayerConst.SpellPvpRulesEnabled))
                return;

            if (enabled)
            {
                SetPlayerFlag(PlayerFlags.WarModeDesired);
                SetPvP(true);
            }
            else
            {
                RemovePlayerFlag(PlayerFlags.WarModeDesired);
                SetPvP(false);
            }

            UpdateWarModeAuras();
        }

        void SetWarModeLocal(bool enabled)
        {
            if (enabled)
                SetPlayerLocalFlag(PlayerLocalFlags.WarMode);
            else
                RemovePlayerLocalFlag(PlayerLocalFlags.WarMode);
        }

        public bool CanEnableWarModeInArea()
        {
            var zone = CliDB.AreaTableStorage.LookupByKey(GetZoneId());
            if (zone == null || !IsFriendlyArea(zone))
                return false;

            var area = CliDB.AreaTableStorage.LookupByKey(GetAreaId());
            if (area == null)
                area = zone;

            do
            {
                if (area.HasFlag(AreaFlags2.AllowWarModeToggle))
                    return true;

                area = CliDB.AreaTableStorage.LookupByKey(area.ParentAreaID);
            }
            while (area != null);

            return false;
        }

        void UpdateWarModeAuras()
        {
            var auraInside = 282559;
            var auraOutside = PlayerConst.WarmodeEnlistedSpellOutside;

            if (IsWarModeDesired())
            {
                if (CanEnableWarModeInArea())
                {
                    RemovePlayerFlag(PlayerFlags.WarModeActive);
                    CastSpell(this, auraInside, true);
                    RemoveAurasDueToSpell(auraOutside);
                    RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.WarModeLeave);
                }
                else
                {
                    SetPlayerFlag(PlayerFlags.WarModeActive);
                    CastSpell(this, auraOutside, true);
                    RemoveAurasDueToSpell(auraInside);
                }
                SetWarModeLocal(true);
                SetPvpFlag(UnitPVPStateFlags.PvP);
            }
            else
            {
                SetWarModeLocal(false);
                RemoveAurasDueToSpell(auraOutside);
                RemoveAurasDueToSpell(auraInside);
                RemovePlayerFlag(PlayerFlags.WarModeActive);
                if (!HasPlayerFlag(PlayerFlags.InPVP))
                    RemovePvpFlag(UnitPVPStateFlags.PvP);
                RemoveAurasWithInterruptFlags(SpellAuraInterruptFlags2.WarModeLeave);
            }
        }

        bool IsWarModeDesired() { return HasPlayerFlag(PlayerFlags.WarModeDesired); }
        bool IsWarModeActive() { return HasPlayerFlag(PlayerFlags.WarModeActive); }
        public bool IsWarModeLocalActive() { return HasPlayerLocalFlag(PlayerLocalFlags.WarMode); }

        // Used in triggers for check "Only to targets that grant experience or honor" req
        public bool IsHonorOrXPTarget(Unit victim)
        {
            var v_level = victim.GetLevelForTarget(this);
            var k_grey = Formulas.GetGrayLevel(GetLevel());

            // Victim level less gray level
            if (v_level < k_grey && WorldConfig.Values[WorldCfg.MinCreatureScaledXpRatio].Int32 == 0)
                return false;

            Creature creature = victim.ToCreature();
            if (creature != null)
            {
                if (creature.IsCritter() || creature.IsTotem())
                    return false;
            }
            return true;
        }

        public void SetRegenTimerCount(Milliseconds time) { m_regenTimerCount = time; }
        void SetWeaponChangeTimer(Milliseconds time) { m_weaponChangeTimer = time; }

        //Team
        public static Team TeamForRace(Race race)
        {
            switch (TeamIdForRace(race))
            {
                case 0:
                    return Team.Alliance;
                case 1:
                    return Team.Horde;
            }

            return Team.Alliance;
        }

        public static int TeamIdForRace(Race race)
        {
            ChrRacesRecord rEntry = CliDB.ChrRacesStorage.LookupByKey((int)race);
            if (rEntry != null)
                return rEntry.Alliance;

            Log.outError(LogFilter.Player, $"Race ({race}) not found in DBC: wrong DBC files?");
            return BattleGroundTeamId.Neutral;
        }

        public Team GetTeam() { return m_team; }
        public int GetBatttleGroundTeamId() { return m_team == Team.Alliance ? BattleGroundTeamId.Alliance : BattleGroundTeamId.Horde; }

        public Team GetEffectiveTeam() { return HasPlayerFlagEx(PlayerFlagsEx.MercenaryMode) ? (GetTeam() == Team.Alliance ? Team.Horde : Team.Alliance) : GetTeam(); }
        public int GetEffectiveTeamId() { return GetEffectiveTeam() == Team.Alliance ? BattleGroundTeamId.Alliance : BattleGroundTeamId.Horde; }

        //Money
        public long GetMoney() { return m_activePlayerData.Coinage; }

        public bool HasEnoughMoney(long amount)
        {
            if (amount > 0)
                return (GetMoney() >= amount);
            return true;
        }

        public bool ModifyMoney(long amount, bool sendError = true)
        {
            if (amount == 0)
                return true;

            ScriptMgr.OnPlayerMoneyChanged(this, amount);

            if (amount < 0)
                SetMoney(GetMoney() > -amount ? GetMoney() + amount : 0);
            else
            {
                if (GetMoney() <= (PlayerConst.MaxMoneyAmount - amount))
                    SetMoney(GetMoney() + amount);
                else
                {
                    if (sendError)
                        SendEquipError(InventoryResult.TooMuchGold);
                    return false;
                }
            }
            return true;
        }

        public void SetMoney(long value)
        {
            bool loading = GetSession().PlayerLoading();

            if (!loading)
                MoneyChanged(value);

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Coinage), value);

            if (!loading)
                UpdateCriteria(CriteriaType.MostMoneyOwned);
        }

        //Target
        // Used for serverside target changes, does not apply to players
        public override void SetTarget(ObjectGuid guid) { }

        public void SetSelection(ObjectGuid guid)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Target), guid);
        }

        //LoginFlag
        public bool HasAtLoginFlag(AtLoginFlags f) { return Convert.ToBoolean(atLoginFlags & f); }
        public void SetAtLoginFlag(AtLoginFlags f) { atLoginFlags |= f; }

        public void RemoveAtLoginFlag(AtLoginFlags flags, bool persist = false)
        {
            atLoginFlags &= ~flags;
            if (persist)
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_REM_AT_LOGIN_FLAG);
                stmt.SetUInt16(0, (ushort)flags);
                stmt.SetInt64(1, GetGUID().GetCounter());

                DB.Characters.Execute(stmt);
            }
        }

        //Guild
        public void SetInGuild(long guildId)
        {
            if (guildId != 0)
            {
                SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.GuildGUID), ObjectGuid.Create(HighGuid.Guild, guildId));
                SetPlayerFlag(PlayerFlags.GuildLevelEnabled);
            }
            else
            {
                SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.GuildGUID), ObjectGuid.Empty);
                RemovePlayerFlag(PlayerFlags.GuildLevelEnabled);
            }

            CharacterCacheStorage.UpdateCharacterGuildId(GetGUID(), guildId);
        }

        public void SetGuildRank(byte rankId) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.GuildRankID), rankId); }
        public int GetGuildRank() { return m_playerData.GuildRankID; }
        public void SetGuildLevel(int level) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.GuildLevel), level); }
        public int GetGuildLevel() { return m_playerData.GuildLevel; }
        public void SetGuildIdInvited(long GuildId) { m_GuildIdInvited = GuildId; }
        public long GetGuildId() { return ((ObjectGuid)m_unitData.GuildGUID).GetCounter(); }

        public Guild GetGuild()
        {
            long guildId = GetGuildId();
            return guildId != 0 ? GuildMgr.GetGuildById(guildId) : null;
        }

        public long GetGuildIdInvited() { return m_GuildIdInvited; }

        public string GetGuildName()
        {
            return GetGuildId() != 0 ? GuildMgr.GetGuildById(GetGuildId()).GetName() : "";
        }

        public void SetFreePrimaryProfessions(int profs)
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CharacterPoints), profs);
        }

        public void GiveLevel(int level)
        {
            var oldLevel = GetLevel();
            if (level == oldLevel)
                return;

            Guild guild = GetGuild();
            if (guild != null)
                guild.UpdateMemberData(this, GuildMemberData.Level, level);

            PlayerLevelInfo levelInfo = ObjectMgr.GetPlayerLevelInfo(GetRace(), GetClass(), level);

            LevelUpInfo packet = new();
            packet.Level = level;
            packet.HealthDelta = levelInfo.baseHealth - GetCreateHealth();

            // @todo find some better solution
            packet.PowerDelta[0] = levelInfo.baseMana - GetCreateMana(); // [0] is PowerType value? or dynamic powerindex?

            for (Stats i = Stats.Strength; i < Stats.Max; ++i)
                packet.StatDelta[(int)i] = levelInfo.stats[(int)i] - (int)GetCreateStat(i);

            packet.NumNewTalents = DB2Mgr.GetNumTalentsAtLevel(level, GetClass()) - DB2Mgr.GetNumTalentsAtLevel(oldLevel, GetClass());

            SendPacket(packet);

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.NextLevelXP), ObjectMgr.GetXPForLevel(level));

            //update level, max level of skills
            m_PlayedTimeLevel = Seconds.Zero;        // Level Played Time reset

            _ApplyAllLevelScaleItemMods(false);

            SetLevel(level, false);

            UpdateSkillsForLevel();
            LearnDefaultSkills();

            // save base values (bonuses already included in stored stats
            for (var i = Stats.Strength; i < Stats.Max; ++i)
                SetCreateStat(i, levelInfo.stats[(int)i]);

            SetCreateHealth(levelInfo.baseHealth);
            SetCreateMana(levelInfo.baseMana);

            InitGlyphsForLevel();
            InitTalentForLevel();
            InitTaxiNodesForLevel();

            UpdateAllStats();

            _ApplyAllLevelScaleItemMods(true); // Moved to above SetFullHealth so player will have full health from Heirlooms            

            // Only health and mana are set to maximum.
            SetFullHealth();
            foreach (PowerTypeRecord powerType in CliDB.PowerTypeStorage.Values)
            {
                if (powerType.HasFlag(PowerTypeFlags.SetToMaxOnLevelUp))
                    SetFullPower(powerType.PowerTypeEnum);
            }

            // update level to hunter/summon pet
            Pet pet = GetPet();
            if (pet != null)
                pet.SynchronizeLevelWithOwner();

            MailLevelReward mailReward = ObjectMgr.GetMailLevelReward(level, GetRace());
            if (mailReward != null)
            {
                //- TODO: Poor design of mail system
                SQLTransaction trans = new();
                new MailDraft(mailReward.mailTemplateId).SendMailTo(trans, this, new MailSender(MailMessageType.Creature, mailReward.senderEntry));
                DB.Characters.CommitTransaction(trans);
            }

            StartCriteria(CriteriaStartEvent.ReachLevel, level);
            UpdateCriteria(CriteriaType.ReachLevel);
            UpdateCriteria(CriteriaType.ActivelyReachLevel, level);
            if (level > oldLevel)
                UpdateCriteria(CriteriaType.GainLevels, level - oldLevel);

            PushQuests();

            ScriptMgr.OnPlayerLevelChanged(this, (byte)oldLevel);
        }

        public bool CanParry()
        {
            return m_canParry;
        }

        public bool CanBlock()
        {
            return m_canBlock;
        }

        public void ToggleAFK()
        {
            if (IsAFK())
                RemovePlayerFlag(PlayerFlags.AFK);
            else
                SetPlayerFlag(PlayerFlags.AFK);

            // afk player not allowed in Battleground
            if (!IsGameMaster() && IsAFK() && InBattleground() && !InArena())
                LeaveBattleground();
        }

        public void ToggleDND()
        {
            if (IsDND())
                RemovePlayerFlag(PlayerFlags.DND);
            else
                SetPlayerFlag(PlayerFlags.DND);
        }

        public bool IsAFK() { return HasPlayerFlag(PlayerFlags.AFK); }
        public bool IsDND() { return HasPlayerFlag(PlayerFlags.DND); }

        public bool IsMaxLevel()
        {
            return GetLevel() >= m_activePlayerData.MaxLevel;
        }

        public ChatFlags GetChatFlags()
        {
            ChatFlags tag = ChatFlags.None;

            if (IsGMChat())
                tag |= ChatFlags.GM;
            if (IsDND())
                tag |= ChatFlags.DND;
            if (IsAFK())
                tag |= ChatFlags.AFK;
            if (IsDeveloper())
                tag |= ChatFlags.Dev;

            return tag;
        }

        public void InitDisplayIds()
        {
            ChrModelRecord model = DB2Mgr.GetChrModel(GetRace(), GetNativeGender());
            if (model == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player {GetGUID()} has incorrect race/gender pair. Can't init display ids.");
                return;
            }

            SetDisplayId(model.DisplayID, true);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.StateAnimID), DB2Mgr.GetEmptyAnimStateID());
        }

        //Creature
        public Creature GetNPCIfCanInteractWith(ObjectGuid guid, NPCFlags1 npcFlags, NPCFlags2 npcFlags2)
        {
            // unit checks
            if (guid.IsEmpty())
                return null;

            if (!IsInWorld)
                return null;

            if (IsInFlight())
                return null;

            // exist (we need look pets also for some interaction (quest/etc)
            Creature creature = ObjectAccessor.GetCreatureOrPetOrVehicle(this, guid);
            if (creature == null)
                return null;

            // Deathstate checks
            if (!IsAlive() && !creature.GetCreatureDifficulty().TypeFlags.HasFlag(CreatureTypeFlags.VisibleToGhosts))
                return null;

            // alive or spirit healer
            if (!creature.IsAlive() && !creature.GetCreatureDifficulty().TypeFlags.HasFlag(CreatureTypeFlags.InteractWhileDead))
                return null;

            // appropriate npc Type
            bool hasNpcFlags()
            {
                if (npcFlags == 0 && npcFlags2 == 0)
                    return true;
                if (creature.HasNpcFlag(npcFlags))
                    return true;
                if (creature.HasNpcFlag2(npcFlags2))
                    return true;
                return false;
            };

            if (!hasNpcFlags())
                return null;

            // not allow interaction under control, but allow with own pets
            if (!creature.GetCharmerGUID().IsEmpty())
                return null;

            // not unfriendly/hostile
            if (!creature.IsInteractionAllowedWhileHostile() && creature.GetReactionTo(this) <= ReputationRank.Unfriendly)
                return null;

            if (creature.IsInCombat() && !creature.IsInteractionAllowedInCombat())
                return null;

            // not too far, taken from CGGameUI::SetInteractTarget
            if (!creature.IsWithinDistInMap(this, creature.GetCombatReach() + 4.0f))
                return null;

            return creature;
        }

        public GameObject GetGameObjectIfCanInteractWith(ObjectGuid guid)
        {
            if (guid.IsEmpty())
                return null;

            if (!IsInWorld)
                return null;

            if (IsInFlight())
                return null;

            // exist
            GameObject go = ObjectAccessor.GetGameObject(this, guid);
            if (go == null)
                return null;

            // Players cannot interact with gameobjects that use the "Point" icon
            if (go.GetGoInfo().IconName == "Point")
                return null;

            if (!go.IsWithinDistInMap(this))
                return null;

            return go;
        }

        public GameObject GetGameObjectIfCanInteractWith(ObjectGuid guid, GameObjectTypes type)
        {
            GameObject go = GetGameObjectIfCanInteractWith(guid);
            if (go == null)
                return null;

            if (go.GetGoType() != type)
                return null;

            return go;
        }

        public void SendInitialPacketsBeforeAddToMap()
        {
            if (!m_teleport_options.HasAnyFlag(TeleportToOptions.Seamless))
            {
                m_movementCounter = 0;
                GetSession().ResetTimeSync();
            }

            GetSession().SendTimeSync();

            GetSocial().SendSocialList(this, SocialFlag.All);

            // SMSG_BINDPOINTUPDATE
            SendBindPointUpdate();

            // SMSG_SET_PROFICIENCY
            // SMSG_SET_PCT_SPELL_MODIFIER
            // SMSG_SET_FLAT_SPELL_MODIFIER

            // SMSG_TALENTS_INFO
            SendTalentsInfoData();

            // SMSG_INITIAL_SPELLS
            SendKnownSpells();

            // SMSG_SEND_UNLEARN_SPELLS
            SendUnlearnSpells();

            // SMSG_SEND_SPELL_HISTORY
            SendSpellHistory sendSpellHistory = new();
            GetSpellHistory().WritePacket(sendSpellHistory);
            SendPacket(sendSpellHistory);

            // SMSG_SEND_SPELL_CHARGES
            SendSpellCharges sendSpellCharges = new();
            GetSpellHistory().WritePacket(sendSpellCharges);
            SendPacket(sendSpellCharges);

            ActiveGlyphs activeGlyphs = new();
            foreach (var glyphId in GetGlyphs(GetActiveTalentGroup()))
            {
                var bindableSpells = DB2Mgr.GetGlyphBindableSpells(glyphId);
                foreach (var bindableSpell in bindableSpells)
                {
                    if (HasSpell(bindableSpell) && !m_overrideSpells.ContainsKey(bindableSpell))
                        activeGlyphs.Glyphs.Add(new GlyphBinding(bindableSpell, (ushort)glyphId));
                }
            }

            activeGlyphs.IsFullUpdate = true;
            SendPacket(activeGlyphs);

            // SMSG_ACTION_BUTTONS
            SendInitialActionButtons();

            // SMSG_INITIALIZE_FACTIONS
            reputationMgr.SendInitialReputations();

            // SMSG_SETUP_CURRENCY
            SendCurrencies();

            // SMSG_EQUIPMENT_SET_LIST
            SendEquipmentSetList();

            m_achievementSys.SendAllData(this);
            m_questObjectiveCriteriaMgr.SendAllData(this);

            // SMSG_LOGIN_SETTIMESPEED
            float TimeSpeed = 0.01666667f;
            LoginSetTimeSpeed loginSetTimeSpeed = new();
            loginSetTimeSpeed.NewSpeed = TimeSpeed;
            loginSetTimeSpeed.GameTime = LoopTime.RealmTime;
            loginSetTimeSpeed.ServerTime = LoopTime.ServerTime;
            loginSetTimeSpeed.GameTimeHolidayOffset = Time.Zero; // @todo : 268529800 - got from some packet at 03/13/2024 18:42:00 game and the same server time
            loginSetTimeSpeed.ServerTimeHolidayOffset = Time.Zero; // - always zero even when GameTimeHolidayOffset is set
            SendPacket(loginSetTimeSpeed);

            // SMSG_WORLD_SERVER_INFO
            WorldServerInfo worldServerInfo = new();
            var mapDifficulty = GetMap().GetMapDifficulty();
            if (mapDifficulty != null)
                worldServerInfo.InstanceGroupSize = mapDifficulty.MaxPlayers;
            
            worldServerInfo.IsTournamentRealm = false;             // @todo
            worldServerInfo.RestrictedAccountMaxLevel = null; // @todo
            worldServerInfo.RestrictedAccountMaxMoney = null; // @todo
            worldServerInfo.DifficultyID = GetMap().GetDifficultyID();
            // worldServerInfo.XRealmPvpAlert;  // @todo
            SendPacket(worldServerInfo);

            // Spell modifiers
            SendSpellModifiers();

            // SMSG_ACCOUNT_MOUNT_UPDATE
            AccountMountUpdate mountUpdate = new();
            mountUpdate.IsFullUpdate = true;
            mountUpdate.Mounts = GetSession().GetCollectionMgr().GetAccountMounts();
            SendPacket(mountUpdate);

            // SMSG_ACCOUNT_TOYS_UPDATE
            AccountToyUpdate toyUpdate = new();
            toyUpdate.IsFullUpdate = true;
            toyUpdate.Toys = GetSession().GetCollectionMgr().GetAccountToys();
            SendPacket(toyUpdate);

            // SMSG_ACCOUNT_HEIRLOOM_UPDATE
            AccountHeirloomUpdate heirloomUpdate = new();
            heirloomUpdate.IsFullUpdate = true;
            heirloomUpdate.Heirlooms = GetSession().GetCollectionMgr().GetAccountHeirlooms();
            SendPacket(heirloomUpdate);

            GetSession().GetCollectionMgr().SendFavoriteAppearances();

            InitialSetup initialSetup = new();
            initialSetup.ServerExpansionLevel = (Expansion)WorldConfig.Values[WorldCfg.Expansion].Int32;
            SendPacket(initialSetup);

            SetMovedUnit(this);
        }

        public void SendInitialPacketsAfterAddToMap()
        {
            UpdateVisibilityForPlayer();

            // update zone
            int newzone, newarea;
            GetZoneAndAreaId(out newzone, out newarea);
            UpdateZone(newzone, newarea);                            // also call SendInitWorldStates();

            GetSession().SendLoadCUFProfiles();

            CastSpell(this, 836, true);                             // LOGINEFFECT

            // set some aura effects that send packet to player client after add player to map
            // SendMessageToSet not send it to player not it map, only for aura that not changed anything at re-apply
            // same auras state lost at far teleport, send it one more time in this case also
            AuraType[] auratypes =
            [
                AuraType.ModFear, AuraType.Transform, AuraType.WaterWalk,
                AuraType.FeatherFall, AuraType.Hover, AuraType.SafeFall,
                AuraType.Fly, AuraType.ModIncreaseMountedFlightSpeed, AuraType.None
            ];
            foreach (var aura in auratypes)
            {
                var auraList = GetAuraEffectsByType(aura);
                if (!auraList.Empty())
                    auraList.First().HandleEffect(this, AuraEffectHandleModes.SendForClient, true);
            }

            if (HasAuraType(AuraType.ModStun) || HasAuraType(AuraType.ModStunDisableGravity))
                SetRooted(true);

            MoveSetCompoundState setCompoundState = new();
            // manual send package (have code in HandleEffect(this, AURA_EFFECT_HANDLE_SEND_FOR_CLIENT, true); that must not be re-applied.
            if (HasAuraType(AuraType.ModRoot) || HasAuraType(AuraType.ModRoot2) || HasAuraType(AuraType.ModRootDisableGravity))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveRoot, m_movementCounter++));

            if (HasAuraType(AuraType.FeatherFall))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveSetFeatherFall, m_movementCounter++));

            if (HasAuraType(AuraType.WaterWalk))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveSetWaterWalk, m_movementCounter++));

            if (HasAuraType(AuraType.Hover))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveSetHovering, m_movementCounter++));

            if (HasAuraType(AuraType.ModRootDisableGravity) || HasAuraType(AuraType.ModStunDisableGravity))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveDisableGravity, m_movementCounter++));

            if (HasAuraType(AuraType.CanTurnWhileFalling))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveSetCanTurnWhileFalling, m_movementCounter++));

            if (HasAura(196055)) //DH DoubleJump
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveEnableDoubleJump, m_movementCounter++));

            if (HasAuraType(AuraType.IgnoreMovementForces))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveSetIgnoreMovementForces, m_movementCounter++));

            if (HasAuraType(AuraType.DisableInertia))
                setCompoundState.StateChanges.Add(new MoveSetCompoundState.MoveStateChange(ServerOpcodes.MoveDisableInertia, m_movementCounter++));

            if (!setCompoundState.StateChanges.Empty())
            {
                setCompoundState.MoverGUID = GetGUID();
                SendPacket(setCompoundState);
            }

            SendAurasForTarget(this);
            SendEnchantmentDurations();                             // must be after add to map
            SendItemDurations();                                    // must be after add to map

            //raid downscaling -send difficulty to player
            if (GetMap().IsRaid())
            {
                Difficulty mapDifficulty = GetMap().GetDifficultyID();
                var difficulty = CliDB.DifficultyStorage.LookupByKey(mapDifficulty);
                SendRaidDifficulty(difficulty.HasFlag(DifficultyFlags.Legacy), (int)mapDifficulty);
            }
            else if (GetMap().IsNonRaidDungeon())
                SendDungeonDifficulty((int)GetMap().GetDifficultyID());

            PhasingHandler.OnMapChange(this);

            UpdateItemLevelAreaBasedScaling();

            if (!GetPlayerSharingQuest().IsEmpty())
            {
                Quest quest = ObjectMgr.GetQuestTemplate(GetSharedQuestID());
                if (quest != null)
                    PlayerTalkClass.SendQuestGiverQuestDetails(quest, GetGUID(), true, false);
                else
                    ClearQuestSharingInfo();
            }

            GetSceneMgr().TriggerDelayedScenes();
        }

        public void AddSpellCategoryCooldownMod(int spellCategoryId, int mod)
        {
            int categoryIndex = m_activePlayerData.CategoryCooldownMods.FindIndexIf(mod => mod.SpellCategoryID == spellCategoryId);

            if (categoryIndex < 0)
            {
                CategoryCooldownMod newMod = new();
                newMod.SpellCategoryID = spellCategoryId;
                newMod.ModCooldown = -mod;

                AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CategoryCooldownMods), newMod);
            }
            else
            {
                CategoryCooldownMod g = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CategoryCooldownMods, categoryIndex);
                SetUpdateFieldValue(ref g.ModCooldown, m_activePlayerData.CategoryCooldownMods[categoryIndex].ModCooldown - mod);
            }
        }

        public void RemoveSpellCategoryCooldownMod(int spellCategoryId, int mod)
        {
            int categoryIndex = m_activePlayerData.CategoryCooldownMods.FindIndexIf(mod => mod.SpellCategoryID == spellCategoryId);

            if (categoryIndex < 0)
                return;

            if (m_activePlayerData.CategoryCooldownMods[categoryIndex].ModCooldown + mod == 0)
            {
                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CategoryCooldownMods), categoryIndex);
            }
            else
            {
                CategoryCooldownMod g = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CategoryCooldownMods, categoryIndex);
                SetUpdateFieldValue(ref g.ModCooldown, m_activePlayerData.CategoryCooldownMods[categoryIndex].ModCooldown + mod);
            }
        }

        public void RemoveSocial()
        {
            SocialMgr.RemovePlayerSocial(GetGUID());
            m_social = null;
        }

        public void SaveRecallPosition()
        {
            m_recall_location = new WorldLocation(this);
            m_recall_instanceId = GetInstanceId();
        }

        public void Recall() { TeleportTo(m_recall_location, 0, m_recall_instanceId); }

        public Milliseconds GetSaveTimer() { return m_nextSave; }

        void SetSaveTimer(Milliseconds timer) { m_nextSave = timer; }

        void SendAurasForTarget(Unit target)
        {
            if (target == null || target.GetVisibleAuras().Empty())                  // speedup things
                return;

            var visibleAuras = target.GetVisibleAuras();

            AuraUpdate update = new();
            update.UpdateAll = true;
            update.UnitGUID = target.GetGUID();

            foreach (var auraApp in visibleAuras)
            {
                AuraInfo auraInfo = new();
                auraApp.BuildUpdatePacket(ref auraInfo, false);
                update.Auras.Add(auraInfo);
            }

            SendPacket(update);
        }

        public void InitStatsForLevel(bool reapplyMods = false)
        {
            if (reapplyMods)                                        //reapply stats values only on .reset stats (level) command
                _RemoveAllStatBonuses();

            PlayerLevelInfo levelInfo = ObjectMgr.GetPlayerLevelInfo(GetRace(), GetClass(), GetLevel());

            int exp_max_lvl = (int)ObjectMgr.GetMaxLevelForExpansion(GetSession().GetExpansion());
            int conf_max_lvl = WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32;
            if (exp_max_lvl == SharedConst.DefaultMaxPlayerLevel || exp_max_lvl >= conf_max_lvl)
                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.MaxLevel), conf_max_lvl);
            else
                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.MaxLevel), exp_max_lvl);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.NextLevelXP), ObjectMgr.GetXPForLevel(GetLevel()));
            if (m_activePlayerData.XP >= m_activePlayerData.NextLevelXP)
                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.XP), m_activePlayerData.NextLevelXP - 1);

            // reset before any aura state sources (health set/aura apply)
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.AuraState), 0u);

            UpdateSkillsForLevel();

            // set default cast time multiplier
            SetModCastingSpeed(1.0f);
            SetModSpellHaste(1.0f);
            SetModHaste(1.0f);
            SetModRangedHaste(1.0f);
            SetModHasteRegen(1.0f);
            SetModTimeRate(1.0f);

            // reset size before reapply auras
            SetObjectScale(1.0f);

            // save base values (bonuses already included in stored stats
            for (var i = Stats.Strength; i < Stats.Max; ++i)
                SetCreateStat(i, levelInfo.stats[(int)i]);

            for (var i = Stats.Strength; i < Stats.Max; ++i)
                SetStat(i, levelInfo.stats[(int)i]);

            //set create powers
            SetCreateHealth(levelInfo.baseHealth);            
            SetCreateMana(levelInfo.baseMana);

            SetArmor((int)(GetCreateStat(Stats.Agility) * 2));

            //reset rating fields values
            for (int index = 0; index < (int)CombatRating.Max; ++index)
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CombatRatings, index), 0);

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePos), 0);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingPercent), 1.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModPeriodicHealingDonePercent), 1.0f);
            for (byte i = 0; i < (int)SpellSchools.Max; ++i)
            {
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDoneNeg, i), 0);
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePos, i), 0);
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModDamageDonePercent, i), 1.0f);                
            }

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModHealingDonePercent), 1.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModSpellPowerPercent), 1.0f);

            //reset attack power, damage and attack speed fields
            for (WeaponAttackType attackType = 0; attackType < WeaponAttackType.Max; ++attackType)
                SetBaseAttackTime(attackType, SharedConst.BaseAttackTime);

            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinDamage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxDamage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinOffHandDamage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxOffHandDamage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MinRangedDamage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.MaxRangedDamage), 0.0f);

            for (int i = 0; i < (int)WeaponAttackType.Max; ++i)
            {
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.WeaponDmgMultipliers, i), 1.0f);
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.WeaponAtkSpeedMultipliers, i), 1.0f);
            }

            SetAttackPower(0);
            SetAttackPowerMultiplier(0.0f);
            SetRangedAttackPower(0);
            SetRangedAttackPowerMultiplier(0.0f);

            // Base crit values (will be recalculated in UpdateAllStats() at loading and in _ApplyAllStatBonuses() at reset
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CritPercentage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OffhandCritPercentage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.RangedCritPercentage), 0.0f);

            // Init spell schools (will be recalculated in UpdateAllStats() at loading and in _ApplyAllStatBonuses() at reset
            for (var spellSchool = SpellSchools.Holy; spellSchool < SpellSchools.Max; ++spellSchool)
                SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SpellCritPercentage, (int)spellSchool), 0.0f);

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ParryPercentage), 0.0f);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.BlockPercentage), 0.0f);

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ShieldBlock), 0);

            // Dodge percentage
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.DodgePercentage), 0.0f);

            // set armor (resistance 0) to original value (create_agility*2)
            SetArmor((int)(GetCreateStat(Stats.Agility) * 2));
            // set other resistance to original value (0)
            for (var spellSchool = SpellSchools.Holy; spellSchool < SpellSchools.Max; ++spellSchool)
            {
                SetResistance(spellSchool, 0);
            }

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModTargetResistance), 0);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ModTargetPhysicalResistance), 0);
            
            // Reset no reagent cost field
            SetNoRegentCostMask(new FlagArray128());

            // Init data for form but skip reapply item mods for form
            InitDataForForm(reapplyMods);

            // save new stats
            for (var i = PowerType.Mana; i < PowerType.Max; ++i)
                SetMaxPower(i, GetCreatePowerValue(i));

            SetMaxHealth(0);                     // stamina bonus will applied later

            // cleanup mounted state (it will set correctly at aura loading if player saved at mount.
            SetMountDisplayId(0);

            // cleanup unit flags (will be re-applied if need at aura load).
            RemoveUnitFlag(UnitFlags.NonAttackable | UnitFlags.RemoveClientControl | UnitFlags.NotAttackable1 |
            UnitFlags.ImmuneToPc | UnitFlags.ImmuneToNpc | UnitFlags.Looting |
            UnitFlags.PetInCombat | UnitFlags.Pacified |
            UnitFlags.Stunned | UnitFlags.InCombat | UnitFlags.Disarmed |
            UnitFlags.Confused | UnitFlags.Fleeing | UnitFlags.Uninteractible |
            UnitFlags.Skinnable | UnitFlags.Mount | UnitFlags.OnTaxi);
            SetUnitFlag(UnitFlags.PlayerControlled);   // must be set

            SetUnitFlag2(UnitFlags2.RegeneratePower);// must be set

            // cleanup player flags (will be re-applied if need at aura load), to avoid have ghost flag without ghost aura, for example.
            RemovePlayerFlag(PlayerFlags.AFK | PlayerFlags.DND | PlayerFlags.GM | PlayerFlags.Ghost);

            RemoveVisFlag(UnitVisFlags.All);                 // one form stealth modified bytes
            RemovePvpFlag(UnitPVPStateFlags.FFAPvp | UnitPVPStateFlags.Sanctuary);

            // restore if need some important flags
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LocalRegenFlags), (byte)0);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.AuraVision), (byte)0);

            if (reapplyMods)                                        // reapply stats values only on .reset stats (level) command
                _ApplyAllStatBonuses();

            // set current level health and mana/energy to maximum after applying all mods.
            SetFullHealth();
            SetFullPower(PowerType.Mana);
            SetFullPower(PowerType.Energy);
            if (GetPower(PowerType.Rage) > GetMaxPower(PowerType.Rage))
                SetFullPower(PowerType.Rage);
            SetFullPower(PowerType.Focus);
            SetPower(PowerType.RunicPower, 0);

            // update level to hunter/summon pet
            Pet pet = GetPet();
            if (pet != null)
                pet.SynchronizeLevelWithOwner();
        }

        public void InitDataForForm(bool reapplyMods = false)
        {
            ShapeShiftForm form = GetShapeshiftForm();

            var ssEntry = CliDB.SpellShapeshiftFormStorage.LookupByKey((int)form);
            if (ssEntry != null && ssEntry.CombatRoundTime != 0)
            {
                SetBaseAttackTime(WeaponAttackType.BaseAttack, ssEntry.CombatRoundTime);
                SetBaseAttackTime(WeaponAttackType.OffAttack, ssEntry.CombatRoundTime);
                SetBaseAttackTime(WeaponAttackType.RangedAttack, SharedConst.BaseAttackTime);
            }
            else
                SetRegularAttackTime();

            UpdateDisplayPower();

            // update auras at form change, ignore this at mods reapply (.reset stats/etc) when form not change.
            if (!reapplyMods)
                UpdateEquipSpellsAtFormChange();

            UpdateMeleeAttackPowerAndDamage();
            UpdateRangedAttackPowerAndDamage();
        }

        public ReputationRank GetReputationRank(int faction)
        {
            var factionEntry = CliDB.FactionStorage.LookupByKey(faction);
            return GetReputationMgr().GetRank(factionEntry);
        }

        public ReputationMgr GetReputationMgr()
        {
            return reputationMgr;
        }

        public void SetReputation(int factionentry, int value)
        {
            GetReputationMgr().SetReputation(CliDB.FactionStorage.LookupByKey(factionentry), value);
        }

        public int GetReputation(int factionentry)
        {
            return GetReputationMgr().GetReputation(CliDB.FactionStorage.LookupByKey(factionentry));
        }

        #region Sends / Updates
        void BeforeVisibilityDestroy(WorldObject obj, Player p)
        {
            if (!obj.IsTypeId(TypeId.Unit))
                return;

            if (p.GetPetGUID() == obj.GetGUID() && obj.ToCreature().IsPet())
                ((Pet)obj).Remove(PetSaveMode.NotInSlot, true);
        }

        public void UpdateVisibilityOf(IReadOnlyList<WorldObject> targets)
        {
            if (targets.Empty())
                return;

            UpdateData udata = new(GetMapId());
            List<WorldObject> newVisibleObjects = new();

            foreach (WorldObject target in targets)
            {
                if (target == this)
                    continue;

                switch (target.GetTypeId())
                {
                    case TypeId.Unit:
                        UpdateVisibilityOf(target.ToCreature(), udata, newVisibleObjects);
                        break;
                    case TypeId.Player:
                        UpdateVisibilityOf(target.ToPlayer(), udata, newVisibleObjects);
                        break;
                    case TypeId.GameObject:
                        UpdateVisibilityOf(target.ToGameObject(), udata, newVisibleObjects);
                        break;
                    case TypeId.DynamicObject:
                        UpdateVisibilityOf(target.ToDynamicObject(), udata, newVisibleObjects);
                        break;
                    case TypeId.Corpse:
                        UpdateVisibilityOf(target.ToCorpse(), udata, newVisibleObjects);
                        break;
                    case TypeId.AreaTrigger:
                        UpdateVisibilityOf(target.ToAreaTrigger(), udata, newVisibleObjects);
                        break;
                    case TypeId.SceneObject:
                        UpdateVisibilityOf(target.ToSceneObject(), udata, newVisibleObjects);
                        break;
                    case TypeId.Conversation:
                        UpdateVisibilityOf(target.ToConversation(), udata, newVisibleObjects);
                        break;
                    default:
                        break;
                }
            }

            if (!udata.HasData())
                return;

            udata.BuildPacket(out UpdateObject packet);
            SendPacket(packet);

            foreach (var visibleUnit in newVisibleObjects)
                SendInitialVisiblePackets(visibleUnit);
        }

        public void UpdateVisibilityOf(WorldObject target)
        {
            if (HaveAtClient(target))
            {
                if (!CanSeeOrDetect(target, false, true))
                {
                    if (target.IsTypeId(TypeId.Unit))
                        BeforeVisibilityDestroy(target.ToCreature(), this);

                    if (!target.IsDestroyedObject())
                        target.SendOutOfRangeForPlayer(this);
                    else
                        target.DestroyForPlayer(this);

                    m_clientGUIDs.Remove(target.GetGUID());
                }
            }
            else
            {
                if (CanSeeOrDetect(target, false, true))
                {
                    target.SendUpdateToPlayer(this);
                    m_clientGUIDs.Add(target.GetGUID());

                    // target aura duration for caster show only if target exist at caster client
                    // send data at target visibility change (adding to client)
                    SendInitialVisiblePackets(target);
                }
            }
        }

        public void UpdateVisibilityOf<T>(T target, UpdateData data, List<WorldObject> visibleNow) where T : WorldObject
        {
            if (HaveAtClient(target))
            {
                if (!CanSeeOrDetect(target, false, true))
                {
                    BeforeVisibilityDestroy(target, this);

                    if (!target.IsDestroyedObject())
                        target.BuildOutOfRangeUpdateBlock(data);
                    else
                        target.BuildDestroyUpdateBlock(data);

                    m_clientGUIDs.Remove(target.GetGUID());
                }
            }
            else
            {
                if (CanSeeOrDetect(target, false, true))
                {
                    target.BuildCreateUpdateBlockForPlayer(data, this);
                    m_clientGUIDs.Add(target.GetGUID());
                    visibleNow.Add(target);
                }
            }
        }

        public void SendInitialVisiblePackets(WorldObject target)
        {
            Unit targetUnit = target.ToUnit();
            if (targetUnit != null)
            {
                SendAurasForTarget(targetUnit);
                if (targetUnit.IsAlive())
                {
                    if (targetUnit.HasUnitState(UnitState.MeleeAttacking) && targetUnit.GetVictim() != null)
                        targetUnit.SendMeleeAttackStart(targetUnit.GetVictim());
                }
            }
        }

        public override void UpdateObjectVisibility(bool forced = true)
        {
            // Prevent updating visibility if player is not in world
            // (example: LoadFromDB sets drunkstate which updates invisibility while player is not in map)
            if (!IsInWorld)
                return;

            if (!forced)
                AddToNotify(NotifyFlags.VisibilityChanged);
            else
            {
                base.UpdateObjectVisibility(true);
                UpdateVisibilityForPlayer();
            }
        }

        public void UpdateVisibilityForPlayer()
        {
            // updates visibility of all objects around point of view for current player
            var notifier = new VisibleNotifier(this);
            Cell.VisitAllObjects(seerView, notifier, GetSightRange());
            notifier.SendToSelf();   // send gathered data
        }

        public void SetSeer(WorldObject target) { seerView = target; }

        public override void SendMessageToSetInRange(ServerPacket data, float dist, bool self)
        {
            if (self)
                SendPacket(data);

            PacketSenderRef sender = new(data);
            var notifier = new MessageDistDeliverer(this, sender, dist);
            Cell.VisitWorldObjects(this, notifier, dist);
        }

        void SendMessageToSetInRange(ServerPacket data, float dist, bool self, bool own_team_only, bool required3dDist = false)
        {
            if (self)
                SendPacket(data);

            PacketSenderRef sender = new(data);
            var notifier = new MessageDistDeliverer(this, sender, dist, own_team_only, null, required3dDist);
            Cell.VisitWorldObjects(this, notifier, dist);
        }

        public override void SendMessageToSet(ServerPacket data, Player skipped_rcvr)
        {
            if (skipped_rcvr != this)
                SendPacket(data);

            // we use World.GetMaxVisibleDistance() because i cannot see why not use a distance
            // update: replaced by GetMap().GetVisibilityDistance()
            PacketSenderRef sender = new(data);
            var notifier = new MessageDistDeliverer(this, sender, GetVisibilityRange(), false, skipped_rcvr);
            Cell.VisitWorldObjects(this, notifier, GetVisibilityRange());
        }
        public override void SendMessageToSet(ServerPacket data, bool self)
        {
            SendMessageToSetInRange(data, GetVisibilityRange(), self);
        }

        public override bool UpdatePosition(Position pos, bool teleport = false)
        {
            return UpdatePosition(pos.GetPositionX(), pos.GetPositionY(), pos.GetPositionZ(), pos.GetOrientation(), teleport);
        }

        public override bool UpdatePosition(float x, float y, float z, float orientation, bool teleport = false)
        {
            if (!base.UpdatePosition(x, y, z, orientation, teleport))
                return false;

            // group update
            if (GetGroup() != null)
                SetGroupUpdateFlag(GroupUpdateFlags.Position);

            if (GetTrader() != null && !IsWithinDistInMap(GetTrader(), SharedConst.InteractionDistance))
                GetSession().SendCancelTrade(TradeStatus.TooFarAway);

            CheckAreaExploreAndOutdoor();

            return true;
        }

        void SendCurrencies()
        {
            SetupCurrency packet = new();

            foreach (var (id, currency) in _currencyStorage)
            {
                CurrencyTypesRecord currencyRecord = CliDB.CurrencyTypesStorage.LookupByKey(id);
                if (currencyRecord == null)
                    continue;

                // Check faction
                if ((currencyRecord.IsAlliance && GetTeam() != Team.Alliance) ||
                    (currencyRecord.IsHorde && GetTeam() != Team.Horde))
                    continue;

                // Check award condition
                if (currencyRecord.AwardConditionID != 0)
                {
                    PlayerConditionRecord playerCondition = CliDB.PlayerConditionStorage.LookupByKey(currencyRecord.AwardConditionID);
                    if (playerCondition != null && !ConditionManager.IsPlayerMeetingCondition(this, playerCondition))
                        continue;
                }

                SetupCurrency.Record record = new();
                record.Type = currencyRecord.Id;
                record.Quantity = currency.Quantity;

                if ((currency.WeeklyQuantity / currencyRecord.Scaler) > 0)
                    record.WeeklyQuantity = currency.WeeklyQuantity;

                if (currencyRecord.HasMaxEarnablePerWeek)
                    record.MaxWeeklyQuantity = GetCurrencyWeeklyCap(currencyRecord);

                if (currencyRecord.IsTrackingQuantity)
                    record.TrackedQuantity = currency.TrackedQuantity;

                if (currencyRecord.HasTotalEarned)
                    record.TotalEarned = currency.EarnedQuantity;

                if (currencyRecord.HasMaxQuantity(true))
                    record.MaxQuantity = GetCurrencyMaxQuantity(currencyRecord, true);

                record.Flags = (byte)currency.Flags;
                record.Flags = (byte)(record.Flags & ~(int)CurrencyDbFlags.UnusedFlags);

                packet.Data.Add(record);
            }

            SendPacket(packet);
        }

        public void ResetCurrencyWeekCap()
        {
            for (byte arenaSlot = 0; arenaSlot < 3; arenaSlot++)
            {
                var arenaTeamId = GetArenaTeamId(arenaSlot);
                if (arenaTeamId != 0)
                {
                    ArenaTeam arenaTeam = ArenaTeamMgr.GetArenaTeamById(arenaTeamId);
                    arenaTeam.FinishWeek();                              // set played this week etc values to 0 in memory, too
                    arenaTeam.SaveToDB();                                // save changes
                    arenaTeam.NotifyStatsChanged();                      // notify the players of the changes
                }
            }

            foreach (var currency in _currencyStorage.Values)
            {

                currency.WeeklyQuantity = 0;
                currency.state = PlayerCurrencyState.Changed;
            }

            SendPacket(new ResetWeeklyCurrency());
        }        

        void CheckAreaExploreAndOutdoor()
        {
            if (!IsAlive())
                return;

            if (IsInFlight())
                return;

            if (WorldConfig.Values[WorldCfg.VmapIndoorCheck].Bool)
                RemoveAurasWithAttribute(IsOutdoors() ? SpellAttr0.OnlyIndoors : SpellAttr0.OnlyOutdoors);

            var areaId = GetAreaId();
            if (areaId == 0)
                return;

            var areaEntry = CliDB.AreaTableStorage.LookupByKey(areaId);
            if (areaEntry == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player '{GetName()}' ({GetGUID()}) discovered unknown area " +
                    $"(x: {GetPositionX()} y: {GetPositionY()} z: {GetPositionZ()} map: {GetMapId()}).");
                return;
            }

            int offset = areaEntry.AreaBit / ActivePlayerData.ExploredZonesBits;
            if (offset >= PlayerConst.ExploredZonesSize)
            {
                Log.outError(LogFilter.Player, 
                    $"Wrong area flag {areaId} in map data for (X: {GetPositionX()} Y: {GetPositionY()}) point to " +
                    $"field PLAYER_EXPLORED_ZONES_1 + {offset} ( {offset} must be < {PlayerConst.ExploredZonesSize} ).");
                return;
            }

            ulong val = 1ul << (areaEntry.AreaBit % ActivePlayerData.ExploredZonesBits);
            ulong currFields = m_activePlayerData.ExploredZones[offset];

            if (!currFields.HasAnyFlag(val))
            {
                SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ExploredZones, offset), val);

                UpdateCriteria(CriteriaType.RevealWorldMapOverlay, GetAreaId());

                if (areaEntry.ExplorationLevel > 0)
                {
                    if (IsMaxLevel())
                    {
                        SendExplorationExperience(areaId, 0);
                    }
                    else
                    {
                        int diff = GetLevel() - areaEntry.ExplorationLevel;
                        int XP;
                        if (diff < -5)
                        {
                            XP = (int)(ObjectMgr.GetBaseXP(GetLevel() + 5) * WorldConfig.Values[WorldCfg.RateXpExplore].Float);
                        }
                        else if (diff > 5)
                        {
                            int exploration_percent = 100 - ((diff - 5) * 5);
                            if (exploration_percent < 0)
                                exploration_percent = 0;

                            XP = (int)(ObjectMgr.GetBaseXP(areaEntry.ExplorationLevel) * exploration_percent / 100 * WorldConfig.Values[WorldCfg.RateXpExplore].Float);
                        }
                        else
                        {
                            XP = (int)(ObjectMgr.GetBaseXP(areaEntry.ExplorationLevel) * WorldConfig.Values[WorldCfg.RateXpExplore].Float);
                        }

                        if (WorldConfig.Values[WorldCfg.MinDiscoveredScaledXpRatio].Int32 > 0)
                        {
                            var minScaledXP = (int)(ObjectMgr.GetBaseXP(areaEntry.ExplorationLevel) * WorldConfig.Values[WorldCfg.RateXpExplore].Float) * WorldConfig.Values[WorldCfg.MinDiscoveredScaledXpRatio].Int32 / 100;
                            XP = Math.Max(minScaledXP, XP);
                        }

                        GiveXP(XP, null);
                        SendExplorationExperience(areaId, XP);
                    }
                    Log.outInfo(LogFilter.Player, $"Player {GetGUID()} discovered a new area: {areaId}.");
                }
            }
        }

        public void AddExploredZones(int pos, ulong mask)
        { 
            SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ExploredZones, pos), mask); 
        }

        public void RemoveExploredZones(int pos, ulong mask) 
        { 
            RemoveUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ExploredZones, pos), mask); 
        }

        void SendExplorationExperience(int Area, int Experience)
        {
            SendPacket(new ExplorationExperience(Experience, Area));
        }

        public void SendSysMessage(CypherStrings str, params object[] args)
        {
            string input = ObjectMgr.GetCypherString(str);
            string pattern = @"%(\d+(\.\d+)?)?(d|f|s|u)";

            int count = 0;
            string result = System.Text.RegularExpressions.Regex.Replace(input, pattern, m =>
            {
                return string.Concat("{", count++, "}");
            });

            SendSysMessage(result, args);
        }

        public void SendSysMessage(string str, params object[] args)
        {
            new CommandHandler(_session).SendSysMessage(string.Format(str, args));
        }

        public void SendBuyError(BuyResult msg, Creature creature, int item)
        {
            BuyFailed packet = new();
            packet.VendorGUID = creature != null ? creature.GetGUID() : ObjectGuid.Empty;
            packet.Muid = item;
            packet.Reason = msg;
            SendPacket(packet);
        }

        public void SendSellError(SellResult msg, Creature creature, ObjectGuid guid)
        {
            SellResponse sellResponse = new();
            sellResponse.VendorGUID = creature != null ? creature.GetGUID() : ObjectGuid.Empty;
            sellResponse.ItemGUIDs.Add(guid);
            sellResponse.Reason = msg;
            SendPacket(sellResponse);
        }
        #endregion

        #region Chat
        public override void Say(string text, Language language, WorldObject obj = null)
        {
            ScriptMgr.OnPlayerChat(this, ChatMsg.Say, language, text);

            SendChatMessageToSetInRange(ChatMsg.Say, language, text, WorldConfig.Values[WorldCfg.ListenRangeSay].Float);
        }

        void SendChatMessageToSetInRange(ChatMsg chatMsg, Language language, string text, float range)
        {
            CustomChatTextBuilder builder = new(this, chatMsg, text, language, this);
            LocalizedDo localizer = new(builder);

            // Send to self
            localizer.Invoke(this);

            // Send to players
            MessageDistDeliverer notifier = new(this, localizer, range, false, null, true);
            Cell.VisitWorldObjects(this, notifier, range);
        }

        public override void Say(int textId, WorldObject target = null)
        {
            Talk(textId, ChatMsg.Say, WorldConfig.Values[WorldCfg.ListenRangeSay].Float, target);
        }

        public override void Yell(string text, Language language, WorldObject obj = null)
        {
            ScriptMgr.OnPlayerChat(this, ChatMsg.Yell, language, text);

            ChatPkt data = new();
            data.Initialize(ChatMsg.Yell, language, this, this, text);
            SendMessageToSetInRange(data, WorldConfig.Values[WorldCfg.ListenRangeYell].Float, true);
        }

        public override void Yell(int textId, WorldObject target = null)
        {
            Talk(textId, ChatMsg.Yell, WorldConfig.Values[WorldCfg.ListenRangeYell].Float, target);
        }

        public override void TextEmote(string text, WorldObject obj = null, bool something = false)
        {
            ScriptMgr.OnPlayerChat(this, ChatMsg.Emote, Language.Universal, text);

            ChatPkt data = new();
            data.Initialize(ChatMsg.Emote, Language.Universal, this, this, text);
            SendMessageToSetInRange(data, WorldConfig.Values[WorldCfg.ListenRangeTextemote].Float, true, 
                !GetSession().HasPermission(RBACPermissions.TwoSideInteractionChat), true);
        }

        public override void TextEmote(int textId, WorldObject target = null, bool isBossEmote = false)
        {
            Talk(textId, ChatMsg.Emote, WorldConfig.Values[WorldCfg.ListenRangeTextemote].Float, target);
        }

        public void WhisperAddon(string text, string prefix, bool isLogged, Player receiver)
        {
            ScriptMgr.OnPlayerChat(this, ChatMsg.Whisper, isLogged ? Language.AddonLogged : Language.Addon, text, receiver);

            if (!receiver.GetSession().IsAddonRegistered(prefix))
                return;

            ChatPkt data = new();
            data.Initialize(ChatMsg.Whisper, isLogged ? Language.AddonLogged : Language.Addon, this, this, text, 0, "", Locale.enUS, prefix);
            receiver.SendPacket(data);
        }

        public override void Whisper(string text, Language language, Player target = null, bool something = false)
        {
            bool isAddonMessage = language == Language.Addon;

            if (!isAddonMessage) // if not addon data
                language = Language.Universal; // whispers should always be readable

            //Player rPlayer = Global.ObjAccessor.FindPlayer(receiver);

            ScriptMgr.OnPlayerChat(this, ChatMsg.Whisper, language, text, target);

            ChatPkt data = new();
            data.Initialize(ChatMsg.Whisper, language, this, this, text);
            target.SendPacket(data);

            // rest stuff shouldn't happen in case of addon message
            if (isAddonMessage)
                return;

            data.Initialize(ChatMsg.WhisperInform, language, target, target, text);
            SendPacket(data);

            if (!IsAcceptWhispers() && !IsGameMaster() && !target.IsGameMaster())
            {
                SetAcceptWhispers(true);
                SendSysMessage(CypherStrings.CommandWhisperon);
            }

            // announce afk or dnd message
            if (target.IsAFK())
                SendSysMessage(CypherStrings.PlayerAfk, target.GetName(), target.autoReplyMsg);
            else if (target.IsDND())
                SendSysMessage(CypherStrings.PlayerDnd, target.GetName(), target.autoReplyMsg);
        }

        public override void Whisper(int textId, Player target, bool isBossWhisper = false)
        {
            if (target == null)
                return;

            BroadcastTextRecord bct = CliDB.BroadcastTextStorage.LookupByKey(textId);
            if (bct == null)
            {
                Log.outError(LogFilter.Unit, $"WorldObject.Whisper: `broadcast_text` was not {textId} found");
                return;
            }

            Locale locale = target.GetSession().GetSessionDbLocaleIndex();
            ChatPkt packet = new();
            packet.Initialize(ChatMsg.Whisper, Language.Universal, this, target, DB2Mgr.GetBroadcastTextValue(bct, locale, GetGender()));
            target.SendPacket(packet);
        }

        public bool CanUnderstandLanguage(Language language)
        {
            if (IsGameMaster())
                return true;

            foreach (var languageDesc in LanguageMgr.GetLanguageDescById(language))
            {
                if (languageDesc.SkillId != 0 && HasSkill((SkillType)languageDesc.SkillId))
                    return true;
            }

            if (HasAuraTypeWithMiscvalue(AuraType.ComprehendLanguage, (int)language))
                return true;

            return false;
        }
        #endregion

        public void ClearWhisperWhiteList() { WhisperList.Clear(); }
        public void AddWhisperWhiteList(ObjectGuid guid) { WhisperList.Add(guid); }
        public bool IsInWhisperWhiteList(ObjectGuid guid) { return WhisperList.Contains(guid); }
        public void RemoveFromWhisperWhiteList(ObjectGuid guid) { WhisperList.Remove(guid); }

        public void SetFallInformation(uint time, float z)
        {
            m_lastFallTime = time;
            m_lastFallZ = z;
        }

        public PlayerCreateMode GetCreateMode() { return m_createMode; }

        public byte GetCinematic() { return m_cinematic; }
        public void SetCinematic(byte cine) { m_cinematic = cine; }

        public int GetMovie() { return m_movie; }
        public void SetMovie(int movie) { m_movie = movie; }

        public void SendCinematicStart(int CinematicSequenceId)
        {
            TriggerCinematic packet = new();
            packet.CinematicID = CinematicSequenceId;
            SendPacket(packet);

            CinematicSequencesRecord sequence = CliDB.CinematicSequencesStorage.LookupByKey(CinematicSequenceId);
            if (sequence != null)
                _cinematicMgr.BeginCinematic(sequence);
        }

        public void SendMovieStart(int movieId)
        {
            SetMovie(movieId);
            TriggerMovie packet = new();
            packet.MovieID = movieId;
            SendPacket(packet);
        }

        public override void SetObjectScale(float scale)
        {
            base.SetObjectScale(scale);
            SetBoundingRadius(scale * SharedConst.DefaultPlayerBoundingRadius);
            SetCombatReach(scale * SharedConst.DefaultPlayerCombatReach);
            if (IsInWorld)
                SendMovementSetCollisionHeight(GetCollisionHeight(), UpdateCollisionHeightReason.Scale);
        }

        public bool HasRaceChanged() { return m_ExtraFlags.HasFlag(PlayerExtraFlags.HasRaceChanged); }
        public void SetHasRaceChanged() { m_ExtraFlags |= PlayerExtraFlags.HasRaceChanged; }
        public bool HasBeenGrantedLevelsFromRaF() { return m_ExtraFlags.HasFlag(PlayerExtraFlags.GrantedLevelsFromRaf); }
        public void SetBeenGrantedLevelsFromRaF() { m_ExtraFlags |= PlayerExtraFlags.GrantedLevelsFromRaf; }
        public bool HasLevelBoosted() { return m_ExtraFlags.HasFlag(PlayerExtraFlags.LevelBoosted); }
        public void SetHasLevelBoosted() { m_ExtraFlags |= PlayerExtraFlags.LevelBoosted; }

        public int GetXP() { return m_activePlayerData.XP; }
        public int GetXPForNextLevel() { return  m_activePlayerData.NextLevelXP; }

        public void SetXP(int xp)
        {
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.XP), xp);

            int playerLevelDelta = 0;

            // If XP < 50%, player should see scaling creature with -1 level except for level max
            if (GetLevel() < SharedConst.MaxLevel && xp < (m_activePlayerData.NextLevelXP / 2))
                playerLevelDelta = -1;

            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ScalingPlayerLevelDelta), playerLevelDelta);
        }

        public void GiveXP(int xp, Unit victim, float group_rate = 1.0f)
        {
            if (xp < 1)
                return;

            if (!IsAlive() && GetBattlegroundId() == 0)
                return;

            if (HasPlayerFlag(PlayerFlags.NoXPGain))
                return;

            if (victim != null && victim.IsTypeId(TypeId.Unit) && !victim.ToCreature().HasLootRecipient())
                return;

            int level = GetLevel();

            ScriptMgr.OnGivePlayerXP(this, ref xp, victim);

            // XP to money conversion processed in Player.RewardQuest
            if (IsMaxLevel())
                return;

            int bonus_xp;
            bool recruitAFriend = GetsRecruitAFriendBonus(true);

            // RaF does NOT stack with rested experience
            if (recruitAFriend)
                bonus_xp = 2 * xp; // xp + bonus_xp must add up to 3 * xp for RaF; calculation for quests done client-side
            else
                bonus_xp = victim != null ? _restMgr.GetRestBonusFor(RestTypes.XP, xp) : 0; // XP resting bonus

            LogXPGain packet = new();
            packet.Victim = victim != null ? victim.GetGUID() : ObjectGuid.Empty;
            packet.Original = xp + bonus_xp;
            packet.Reason = victim != null ? PlayerLogXPReason.Kill : PlayerLogXPReason.NoKill;
            packet.Amount = xp;
            packet.GroupBonus = group_rate;
            SendPacket(packet);

            var nextLvlXP = GetXPForNextLevel();
            var newXP = GetXP() + xp + bonus_xp;

            while (newXP >= nextLvlXP && !IsMaxLevel())
            {
                newXP -= nextLvlXP;

                if (!IsMaxLevel())
                    GiveLevel(level + 1);

                level = GetLevel();
                nextLvlXP = GetXPForNextLevel();
            }

            SetXP(newXP);
        }

        public void HandleBaseModFlatValue(BaseModGroup modGroup, float amount, bool apply)
        {
            if (modGroup >= BaseModGroup.End)
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.HandleBaseModFlatValue: Invalid BaseModGroup ({modGroup}) " +
                    $"for player '{GetName()}' ({GetGUID()})");
                return;
            }
            m_auraBaseFlatMod[(int)modGroup] += apply ? amount : -amount;
            UpdateBaseModGroup(modGroup);
        }

        public void ApplyBaseModPctValue(BaseModGroup modGroup, float pct)
        {
            if (modGroup >= BaseModGroup.End)
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.ApplyBaseModPctValue: Invalid BaseModGroup/BaseModType ({modGroup}/{BaseModType.FlatMod}) " +
                    $"for player '{GetName()}' ({GetGUID()})");
                return;
            }

            MathFunctions.AddPct(ref m_auraBasePctMod[(int)modGroup], pct);
            UpdateBaseModGroup(modGroup);
        }

        public void SetBaseModFlatValue(BaseModGroup modGroup, float val)
        {
            if (m_auraBaseFlatMod[(int)modGroup] == val)
                return;

            m_auraBaseFlatMod[(int)modGroup] = val;
            UpdateBaseModGroup(modGroup);
        }

        public void SetBaseModPctValue(BaseModGroup modGroup, float val)
        {
            if (m_auraBasePctMod[(int)modGroup] == val)
                return;

            m_auraBasePctMod[(int)modGroup] = val;
            UpdateBaseModGroup(modGroup);
        }

        public override void UpdateDamageDoneMods(WeaponAttackType attackType, EnchantmentSlot? skipEnchantSlot = null)
        {
            base.UpdateDamageDoneMods(attackType, skipEnchantSlot);

            UnitMods unitMod = attackType switch
            {
                WeaponAttackType.BaseAttack => UnitMods.DamageMainHand,
                WeaponAttackType.OffAttack => UnitMods.DamageOffHand,
                WeaponAttackType.RangedAttack => UnitMods.DamageRanged,
                _ => throw new NotImplementedException(),
            };

            float amount = 0.0f;
            var item = GetWeaponForAttack(attackType, true);
            if (item == null)
                return;

            for (var slot = EnchantmentSlot.EnhancementPermanent; slot < EnchantmentSlot.Max; ++slot)
            {
                if (skipEnchantSlot.HasValue && skipEnchantSlot.Value == slot)
                    continue;

                var enchantmentEntry = CliDB.SpellItemEnchantmentStorage.LookupByKey(item.GetEnchantmentId(slot));
                if (enchantmentEntry == null)
                    continue;

                for (byte i = 0; i < ItemConst.MaxItemEnchantmentEffects; ++i)
                {
                    switch (enchantmentEntry.Effect(i))
                    {
                        case ItemEnchantmentType.Damage:
                            amount += enchantmentEntry.EffectScalingPoints[i];
                            break;
                        case ItemEnchantmentType.Totem:
                            if (GetClass() == Class.Shaman)
                                amount += enchantmentEntry.EffectScalingPoints[i] * item.GetTemplate().GetDelay() / 1000.0f;
                            break;
                        default:
                            break;
                    }
                }
            }

            StatMods.ModifyFlat(unitMod, (int)amount, true);
        }

        void UpdateBaseModGroup(BaseModGroup modGroup)
        {
            if (!CanModifyStats())
                return;

            switch (modGroup)
            {
                case BaseModGroup.CritPercentage:
                    UpdateCritPercentage(WeaponAttackType.BaseAttack);
                    break;
                case BaseModGroup.RangedCritPercentage:
                    UpdateCritPercentage(WeaponAttackType.RangedAttack);
                    break;
                case BaseModGroup.OffhandCritPercentage:
                    UpdateCritPercentage(WeaponAttackType.OffAttack);
                    break;
                case BaseModGroup.ShieldBlockValue:
                    UpdateShieldBlockValue();
                    break;
                default:
                    break;
            }
        }

        float GetBaseModValue(BaseModGroup modGroup, BaseModType modType)
        {
            if (modGroup >= BaseModGroup.End || modType >= BaseModType.End)
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.GetBaseModValue: Invalid BaseModGroup/BaseModType ({modGroup}/{modType}) " +
                    $"for player '{GetName()}' ({GetGUID()})");
                return 0.0f;
            }

            return (modType == BaseModType.FlatMod ? m_auraBaseFlatMod[(int)modGroup] : m_auraBasePctMod[(int)modGroup]);
        }

        float GetTotalBaseModValue(BaseModGroup modGroup)
        {
            if (modGroup >= BaseModGroup.End)
            {
                Log.outError(LogFilter.Spells, 
                    $"Player.GetTotalBaseModValue: Invalid BaseModGroup ({modGroup}) " +
                    $"for player '{GetName()}' ({GetGUID()})");
                return 0.0f;
            }

            return m_auraBaseFlatMod[(int)modGroup] * m_auraBasePctMod[(int)modGroup];
        }

        public override int GetShieldBlockValue()
        {
            var value = (int)Math.Max(0.0f, (m_auraBaseFlatMod[(int)BaseModGroup.ShieldBlockValue] + GetStat(Stats.Strength) * 0.5f - 10) * m_auraBasePctMod[(int)BaseModGroup.ShieldBlockValue]);
            return value;
        }

        public byte GetDrunkValue() { return m_playerData.Inebriation; }

        public void SetDrunkValue(byte newDrunkValue, int itemId = 0)
        {
            bool isSobering = newDrunkValue < GetDrunkValue();
            DrunkenState oldDrunkenState = GetDrunkenstateByValue(GetDrunkValue());
            if (newDrunkValue > 100)
                newDrunkValue = 100;

            // select drunk percent or total SPELL_AURA_MOD_FAKE_INEBRIATE amount, whichever is higher for visibility updates
            int drunkPercent = Math.Max(newDrunkValue, GetTotalAuraModifier(AuraType.ModFakeInebriate));
            if (drunkPercent != 0)
            {
                m_invisibilityDetect.AddFlag(InvisibilityType.Drunk);
                m_invisibilityDetect.SetValue(InvisibilityType.Drunk, drunkPercent);
            }
            else if (!HasAuraType(AuraType.ModFakeInebriate) && newDrunkValue == 0)
                m_invisibilityDetect.DelFlag(InvisibilityType.Drunk);

            DrunkenState newDrunkenState = GetDrunkenstateByValue(newDrunkValue);
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.Inebriation), newDrunkValue);
            UpdateObjectVisibility();

            if (!isSobering)
                m_drunkTimer = Milliseconds.Zero;   // reset sobering timer

            if (newDrunkenState == oldDrunkenState)
                return;

            CrossedInebriationThreshold data = new();
            data.Guid = GetGUID();
            data.Threshold = newDrunkenState;
            data.ItemID = itemId;

            SendMessageToSet(data, true);
        }

        public static DrunkenState GetDrunkenstateByValue(byte value)
        {
            if (value >= 90)
                return DrunkenState.Smashed;
            if (value >= 50)
                return DrunkenState.Drunk;
            if (value != 0)
                return DrunkenState.Tipsy;
            return DrunkenState.Sober;
        }

        public Milliseconds GetDeathTimer() { return m_deathTimer; }

        public bool ActivateTaxiPathTo(List<int> nodes, Creature npc = null, int spellid = 0, int preferredMountDisplay = 0)
        {
            if (nodes.Count < 2)
            {
                GetSession().SendActivateTaxiReply(ActivateTaxiReply.NoSuchPath);
                return false;
            }

            // not let cheating with start flight in time of logout process || while in combat || has Type state: stunned || has Type state: root
            if (GetSession().IsLogingOut() || IsInCombat() || HasUnitState(UnitState.Stunned) || HasUnitState(UnitState.Root))
            {
                GetSession().SendActivateTaxiReply(ActivateTaxiReply.PlayerBusy);
                return false;
            }

            if (HasUnitFlag(UnitFlags.RemoveClientControl))
                return false;

            // taximaster case
            if (npc != null)
            {
                // not let cheating with start flight mounted
                RemoveAurasByType(AuraType.Mounted);

                if (GetDisplayId() != GetNativeDisplayId())
                    RestoreDisplayId(true);

                if (IsDisallowedMountForm(GetTransformSpell(), ShapeShiftForm.None, GetDisplayId()))
                {
                    GetSession().SendActivateTaxiReply(ActivateTaxiReply.PlayerShapeshifted);
                    return false;
                }

                // not let cheating with start flight in time of logout process || if casting not finished || while in combat || if not use Spell's with EffectSendTaxi
                if (IsNonMeleeSpellCast(false))
                {
                    GetSession().SendActivateTaxiReply(ActivateTaxiReply.PlayerBusy);
                    return false;
                }
            }
            // cast case or scripted call case
            else
            {
                RemoveAurasByType(AuraType.Mounted);

                if (GetDisplayId() != GetNativeDisplayId())
                    RestoreDisplayId(true);

                Spell spell = GetCurrentSpell(CurrentSpellTypes.Generic);
                if (spell != null)
                {
                    if (spell.m_spellInfo.Id != spellid)
                        InterruptSpell(CurrentSpellTypes.Generic, false);
                }

                InterruptSpell(CurrentSpellTypes.AutoRepeat, false);

                spell = GetCurrentSpell(CurrentSpellTypes.Channeled);
                if (spell != null)
                {
                    if (spell.m_spellInfo.Id != spellid)
                        InterruptSpell(CurrentSpellTypes.Channeled, true);
                }
            }

            var sourcenode = nodes[0];

            // starting node too far away (cheat?)
            var node = CliDB.TaxiNodesStorage.LookupByKey(sourcenode);
            if (node == null)
            {
                GetSession().SendActivateTaxiReply(ActivateTaxiReply.NoSuchPath);
                return false;
            }

            // Prepare to flight start now

            // stop combat at start taxi flight if any
            CombatStop();

            StopCastingCharm();
            StopCastingBindSight();
            ExitVehicle();

            // stop trade (client cancel trade at taxi map open but cheating tools can be used for reopen it)
            TradeCancel(true);

            // clean not finished taxi path if any
            m_taxi.ClearTaxiDestinations();

            // 0 element current node
            m_taxi.AddTaxiDestination(sourcenode);

            // fill destinations path tail
            int sourcepath = 0;
            uint totalcost = 0;
            uint firstcost = 0;

            var prevnode = sourcenode;
            var lastnode = 0;

            for (int i = 1; i < nodes.Count; ++i)
            {
                int path;
                uint cost;

                lastnode = nodes[i];
                ObjectMgr.GetTaxiPath(prevnode, lastnode, out path, out cost);

                if (path == 0)
                {
                    m_taxi.ClearTaxiDestinations();
                    return false;
                }

                totalcost += cost;
                if (i == 1)
                    firstcost = cost;

                if (prevnode == sourcenode)
                    sourcepath = path;

                m_taxi.AddTaxiDestination(lastnode);

                prevnode = lastnode;
            }

            // get mount model (in case non taximaster (npc == NULL) allow more wide lookup)
            //
            // Hack-Fix for Alliance not being able to use Acherus taxi. There is
            // only one mount ID for both sides. Probably not good to use 315 in case DBC nodes
            // change but I couldn't find a suitable alternative. OK to use class because only DK
            // can use this taxi.
            int mount_display_id;
            if (node.HasFlag(TaxiNodeFlags.UsePlayerFavoriteMount) && preferredMountDisplay != 0)
                mount_display_id = preferredMountDisplay;
            else
                mount_display_id = ObjectMgr.GetTaxiMountDisplayId(sourcenode, GetTeam(), npc == null || (sourcenode == 315 && GetClass() == Class.DeathKnight));

            // in spell case allow 0 model
            if ((mount_display_id == 0 && spellid == 0) || sourcepath == 0)
            {
                GetSession().SendActivateTaxiReply(ActivateTaxiReply.UnspecifiedServerError);
                m_taxi.ClearTaxiDestinations();
                return false;
            }

            var money = GetMoney();
            if (npc != null)
            {
                float discount = GetReputationPriceDiscount(npc);
                totalcost = (uint)Math.Ceiling(totalcost * discount);
                firstcost = (uint)Math.Ceiling(firstcost * discount);
                m_taxi.SetFlightMasterFactionTemplateId(npc.GetFaction());
            }
            else
                m_taxi.SetFlightMasterFactionTemplateId(0);

            if (money < totalcost)
            {
                GetSession().SendActivateTaxiReply(ActivateTaxiReply.NotEnoughMoney);
                m_taxi.ClearTaxiDestinations();
                return false;
            }

            //Checks and preparations done, DO FLIGHT
            UpdateCriteria(CriteriaType.BuyTaxi, 1);

            if (WorldConfig.Values[WorldCfg.InstantTaxi].Bool)
            {
                var lastPathNode = CliDB.TaxiNodesStorage.LookupByKey(nodes[^1]);
                m_taxi.ClearTaxiDestinations();
                ModifyMoney(-totalcost);
                UpdateCriteria(CriteriaType.MoneySpentOnTaxis, totalcost);
                TeleportTo(lastPathNode.ContinentID, lastPathNode.Pos.X, lastPathNode.Pos.Y, lastPathNode.Pos.Z, GetOrientation());
                return false;
            }
            else
            {
                ModifyMoney(-firstcost);
                UpdateCriteria(CriteriaType.MoneySpentOnTaxis, firstcost);
                GetSession().SendActivateTaxiReply();
                GetSession().SendDoFlight(mount_display_id, sourcepath);
            }
            return true;
        }

        public bool ActivateTaxiPathTo(int taxi_path_id, int spellid = 0)
        {
            var entry = CliDB.TaxiPathStorage.LookupByKey(taxi_path_id);
            if (entry == null)
                return false;

            List<int> nodes = new();

            nodes.Add(entry.FromTaxiNode);
            nodes.Add(entry.ToTaxiNode);

            return ActivateTaxiPathTo(nodes, null, spellid);
        }

        public void FinishTaxiFlight()
        {
            if (!IsInFlight())
                return;

            GetMotionMaster().Remove(MovementGeneratorType.Flight);
            m_taxi.ClearTaxiDestinations(); // not destinations, clear source node
        }

        public void CleanupAfterTaxiFlight()
        {
            m_taxi.ClearTaxiDestinations(); // not destinations, clear source node
            Dismount();
            RemoveUnitFlag(UnitFlags.RemoveClientControl | UnitFlags.OnTaxi);
        }

        public void ContinueTaxiFlight()
        {
            int sourceNode = m_taxi.GetTaxiSource();
            if (sourceNode == 0)
                return;

            Log.outDebug(LogFilter.Unit, $"WORLD: Restart character {GetGUID()} taxi flight");

            int mountDisplayId = ObjectMgr.GetTaxiMountDisplayId(sourceNode, GetTeam(), true);
            if (mountDisplayId == 0)
                return;

            int path = m_taxi.GetCurrentTaxiPath();

            // search appropriate start path node
            int startNode = 0;

            var nodeList = CliDB.TaxiPathNodesByPath[path];

            float distPrev;
            float distNext = GetExactDistSq(nodeList[0].Loc.X, nodeList[0].Loc.Y, nodeList[0].Loc.Z);

            for (int i = 1; i < nodeList.Length; ++i)
            {
                var node = nodeList[i];
                var prevNode = nodeList[i - 1];

                // skip nodes at another map
                if (node.ContinentID != GetMapId())
                    continue;

                distPrev = distNext;

                distNext = GetExactDistSq(node.Loc.X, node.Loc.Y, node.Loc.Z);

                float distNodes =
                    (node.Loc.X - prevNode.Loc.X) * (node.Loc.X - prevNode.Loc.X) +
                    (node.Loc.Y - prevNode.Loc.Y) * (node.Loc.Y - prevNode.Loc.Y) +
                    (node.Loc.Z - prevNode.Loc.Z) * (node.Loc.Z - prevNode.Loc.Z);

                if (distNext + distPrev < distNodes)
                {
                    startNode = i;
                    break;
                }
            }

            GetSession().SendDoFlight(mountDisplayId, path, startNode);
        }

        public bool GetsRecruitAFriendBonus(bool forXP)
        {
            bool recruitAFriend = false;
            if (GetLevel() <= WorldConfig.Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel].Int32 || !forXP)
            {
                Group group = GetGroup();
                if (group != null)
                {
                    for (GroupReference refe = group.GetFirstMember(); refe != null; refe = refe.Next())
                    {
                        Player player = refe.GetSource();
                        if (player == null)
                            continue;

                        if (!player.IsAtRecruitAFriendDistance(this))
                            continue;                               // member (alive or dead) or his corpse at req. distance

                        if (forXP)
                        {
                            // level must be allowed to get RaF bonus
                            if (player.GetLevel() > WorldConfig.Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevel].Int32)
                                continue;

                            // level difference must be small enough to get RaF bonus, UNLESS we are lower level
                            if (player.GetLevel() < GetLevel())
                            {
                                if (GetLevel() - player.GetLevel() > WorldConfig.Values[WorldCfg.MaxRecruitAFriendBonusPlayerLevelDifference].Int32)
                                    continue;
                            }
                        }

                        bool ARecruitedB = (player.GetSession().GetRecruiterId() == GetSession().GetAccountId());
                        bool BRecruitedA = (GetSession().GetRecruiterId() == player.GetSession().GetAccountId());
                        if (ARecruitedB || BRecruitedA)
                        {
                            recruitAFriend = true;
                            break;
                        }
                    }
                }
            }
            return recruitAFriend;
        }

        bool IsAtRecruitAFriendDistance(WorldObject pOther)
        {
            if (pOther == null || !IsInMap(pOther))
                return false;

            WorldObject player = GetCorpse();
            if (player == null || IsAlive())
                player = this;

            return pOther.GetDistance(player) <= WorldConfig.Values[WorldCfg.MaxRecruitAFriendDistance].Float;
        }

        public TeleportToOptions GetTeleportOptions() { return m_teleport_options; }
        public bool IsBeingTeleported() { return IsBeingTeleportedNear() || IsBeingTeleportedFar(); }
        public bool IsBeingTeleportedNear() { return mSemaphoreTeleport_Near; }
        public bool IsBeingTeleportedFar() { return mSemaphoreTeleport_Far; }
        public bool IsBeingTeleportedSeamlessly() { return IsBeingTeleportedFar() && m_teleport_options.HasAnyFlag(TeleportToOptions.Seamless); }
        public void SetSemaphoreTeleportNear(bool semphsetting) { mSemaphoreTeleport_Near = semphsetting; }
        public void SetSemaphoreTeleportFar(bool semphsetting) { mSemaphoreTeleport_Far = semphsetting; }

        //new
        public int DoRandomRoll(int minimum, int maximum)
        {
            Cypher.Assert(maximum <= 1000000);

            int roll = RandomHelper.IRand(minimum, maximum);

            RandomRoll randomRoll = new();
            randomRoll.Min = minimum;
            randomRoll.Max = maximum;
            randomRoll.Result = roll;
            randomRoll.Roller = GetGUID();
            randomRoll.RollerWowAccount = GetSession().GetAccountGUID();

            Group group = GetGroup();
            if (group != null)
                group.BroadcastPacket(randomRoll, false);
            else
                SendPacket(randomRoll);

            return roll;
        }

        public bool IsVisibleGloballyFor(Player u)
        {
            if (u == null)
                return false;

            // Always can see self
            if (u.GetGUID() == GetGUID())
                return true;

            // Visible units, always are visible for all players
            if (IsVisible())
                return true;

            // GMs are visible for higher gms (or players are visible for gms)
            if (!AccountMgr.IsPlayerAccount(u.GetSession().GetSecurity()))
                return GetSession().GetSecurity() <= u.GetSession().GetSecurity();

            // non faction visibility non-breakable for non-GMs
            return false;
        }

        public float GetReputationPriceDiscount(Creature creature)
        {
            return GetReputationPriceDiscount(creature.GetFactionTemplateEntry());
        }

        public float GetReputationPriceDiscount(FactionTemplateRecord factionTemplate)
        {
            if (factionTemplate == null || factionTemplate.Faction == 0)
                return 1.0f;

            ReputationRank rank = GetReputationRank(factionTemplate.Faction);
            if (rank <= ReputationRank.Neutral)
                return 1.0f;

            return 1.0f - 0.05f * (rank - ReputationRank.Neutral);
        }
        public bool IsSpellFitByClassAndRace(int spell_id)
        {
            Class class_ = GetClass();

            var bounds = SpellMgr.GetSkillLineAbilityMapBounds(spell_id);

            if (bounds.Empty())
                return true;

            foreach (var _spell_idx in bounds)
            {
                // skip wrong race skills
                var raceMask = _spell_idx.RaceMask;
                if (raceMask != RaceMask.None && !raceMask.HasRace(GetRace()))
                    continue;

                // skip wrong class skills
                if (_spell_idx.ClassMask != ClassMask.None && (_spell_idx.ClassMask.HasClass(class_)))
                    continue;

                // skip wrong class and race skill saved in SkillRaceClassInfo.dbc
                if (DB2Mgr.GetSkillRaceClassInfo(_spell_idx.SkillLine, GetRace(), GetClass()) == null)
                    continue;

                return true;
            }

            return false;
        }

        void SetActiveCombatTraitConfigID(int traitConfigId) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ActiveCombatTraitConfigID), traitConfigId); 
        }

        void InitPrimaryProfessions()
        {
            SetFreePrimaryProfessions(WorldConfig.Values[WorldCfg.MaxPrimaryTradeSkill].Int32);
        }

        public int GetFreePrimaryProfessionPoints() { return m_activePlayerData.CharacterPoints; }

        void SetFreePrimaryProfessions(ushort profs)
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.CharacterPoints), profs); 
        }

        public bool HaveAtClient(WorldObject u)
        {
            bool one = u.GetGUID() == GetGUID();
            bool two = m_clientGUIDs.Contains(u.GetGUID());

            return one || two;
        }

        public bool HasTitle(CharTitlesRecord title) { return HasTitle(title.MaskID); }

        public bool HasTitle(int bitIndex)
        {
            uint fieldIndexOffset = (uint)bitIndex / 64;
            if (fieldIndexOffset >= m_activePlayerData.KnownTitles.Size())
                return false;

            ulong flag = 1ul << (bitIndex % 64);
            return (m_activePlayerData.KnownTitles[(int)fieldIndexOffset] & flag) != 0;
        }

        public void SetTitle(CharTitlesRecord title, bool lost = false)
        {
            int fieldIndexOffset = (title.MaskID / 64);
            ulong flag = 1ul << (title.MaskID % 64);

            if (lost)
            {
                if (!HasTitle(title))
                    return;

                RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.KnownTitles, fieldIndexOffset), flag);
            }
            else
            {
                if (HasTitle(title))
                    return;

                SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.KnownTitles, fieldIndexOffset), flag);
            }

            TitleEarned packet = new(lost ? ServerOpcodes.TitleLost : ServerOpcodes.TitleEarned);
            packet.Index = title.MaskID;
            SendPacket(packet);
        }

        public void SetChosenTitle(int title) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerTitle), title); 
        }

        public void SetKnownTitles(int index, ulong mask) 
        { 
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.KnownTitles, index), mask); 
        }

        public void SetViewpoint(WorldObject target, bool apply)
        {
            if (apply)
            {
                Log.outDebug(LogFilter.Maps, 
                    $"Player.CreateViewpoint: Player {GetName()} " +
                    $"create seer {target.GetEntry()} (TypeId: {target.GetTypeId()}).");

                if (m_activePlayerData.FarsightObject != ObjectGuid.Empty)
                {
                    Log.outFatal(LogFilter.Player, 
                        $"Player.CreateViewpoint: Player {GetName()} cannot add new viewpoint!");
                    return;

                }

                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.FarsightObject), target.GetGUID());

                // farsight dynobj or puppet may be very far away
                UpdateVisibilityOf(target);

                Unit targetUnit = target.ToUnit();
                if (targetUnit != null && targetUnit != GetVehicleBase())
                    targetUnit.AddPlayerToVision(this);
                SetSeer(target);
            }
            else
            {
                Log.outDebug(LogFilter.Maps, 
                    $"Player.CreateViewpoint: Player {GetName()} remove seer");

                if (target.GetGUID() != m_activePlayerData.FarsightObject)
                {
                    Log.outFatal(LogFilter.Player, 
                        $"Player.CreateViewpoint: Player {GetName()} cannot remove current viewpoint!");
                    return;
                }

                SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.FarsightObject), ObjectGuid.Empty);

                Unit targetUnit = target.ToUnit();
                if (targetUnit != null && targetUnit != GetVehicleBase())
                    targetUnit.RemovePlayerFromVision(this);

                //must immediately set seer back otherwise may crash
                SetSeer(this);
            }
        }

        public WorldObject GetViewpoint()
        {
            ObjectGuid guid = m_activePlayerData.FarsightObject;
            if (!guid.IsEmpty())
                return ObjAccessor.GetObjectByTypeMask(this, guid, TypeMask.Seer);

            return null;
        }

        public void SetClientControl(Unit target, bool allowMove)
        {
            // a player can never client control nothing
            Cypher.Assert(target != null);

            // don't allow possession to be overridden
            if (target.HasUnitState(UnitState.Charmed) && (GetGUID() != target.GetCharmerGUID()))
            {
                // this should never happen, otherwise m_unitBeingMoved might be left dangling!
                Log.outError(LogFilter.Player, 
                    $"Player '{GetName()}' attempt to client control '{target.GetName()}', " +
                    $"which is charmed by GUID {target.GetCharmerGUID()}");
                return;
            }

            // still affected by some aura that shouldn't allow control, only allow on last such aura to be removed
            if (target.HasUnitState(UnitState.Fleeing | UnitState.Confused))
                allowMove = false;

            ControlUpdate packet = new();
            packet.Guid = target.GetGUID();
            packet.On = allowMove;
            SendPacket(packet);

            WorldObject viewpoint = GetViewpoint();
            if (viewpoint == null)
                viewpoint = this;
            if (target != viewpoint)
            {
                if (viewpoint != this)
                    SetViewpoint(viewpoint, false);
                if (target != this)
                    SetViewpoint(target, true);
            }

            SetMovedUnit(target);
        }

        public Item GetWeaponForAttack(WeaponAttackType attackType, bool useable = false)
        {
            byte slot;
            switch (attackType)
            {
                case WeaponAttackType.BaseAttack:
                    slot = EquipmentSlot.MainHand;
                    break;
                case WeaponAttackType.OffAttack:
                    slot = EquipmentSlot.OffHand;
                    break;
                case WeaponAttackType.RangedAttack:
                    slot = EquipmentSlot.Ranged;
                    break;
                case WeaponAttackType.Any:
                    return null;
                default:
                    return null;
            }

            Item item;
            if (useable)
                item = GetUseableItemByPos(slot);
            else
                item = GetItemByPos(slot);

            if (item == null || item.GetTemplate().GetClass() != ItemClass.Weapon)
                return null;

            if (!useable)
                return item;

            if (item.IsBroken() || IsInFeralForm())
                return null;

            return item;
        }

        public static WeaponAttackType? GetAttackBySlot(byte slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => WeaponAttackType.BaseAttack,
                EquipmentSlot.OffHand => WeaponAttackType.OffAttack,
                EquipmentSlot.Ranged => WeaponAttackType.RangedAttack,
                _ => null,
            };
        }

        public void AutoUnequipOffhandIfNeed(bool force = false)
        {
            Item offItem = GetItemByPos(EquipmentSlot.OffHand);
            if (offItem == null)
                return;

            ItemTemplate offtemplate = offItem.GetTemplate();

            // unequip offhand weapon if player doesn't have dual wield anymore
            if (!CanDualWield() && ((offItem.GetTemplate().GetInventoryType() == InventoryType.WeaponOffhand && !offItem.GetTemplate().HasFlag(ItemFlags3.AlwaysAllowDualWield))
                    || offItem.GetTemplate().GetInventoryType() == InventoryType.Weapon))
                force = true;

            // need unequip offhand for 2h-weapon without TitanGrip (in any from hands)
            if (!force && (CanTitanGrip() || (offtemplate.GetInventoryType() != InventoryType.Weapon2Hand && !IsTwoHandUsed())))
                return;

            List<ItemPosCount> off_dest;
            InventoryResult off_msg = CanStoreItem(ItemPos.Undefined, out off_dest, offItem);
            if (off_msg == InventoryResult.Ok)
            {
                RemoveItem(new(EquipmentSlot.OffHand), true);
                StoreItem(off_dest, offItem, true);
            }
            else
            {
                MoveItemFromInventory(new(EquipmentSlot.OffHand), true);
                SQLTransaction trans = new();
                offItem.DeleteFromInventoryDB(trans);                   // deletes item from character's inventory
                offItem.SaveToDB(trans);                                // recursive and not have transaction guard into self, item not in inventory and can be save standalone

                string subject = ObjectMgr.GetCypherString(CypherStrings.NotEquippedItem);
                new MailDraft(subject, "There were problems with equipping one or several items").AddItem(offItem).SendMailTo(trans, this, new MailSender(this, MailStationery.Gm), MailCheckFlags.Copied);

                DB.Characters.CommitTransaction(trans);
            }
        }

        public WorldLocation GetTeleportDest()
        {
            return teleportDest;
        }

        public int? GetTeleportDestInstanceId()
        {
            return m_teleport_instanceId;
        }

        public WorldLocation GetHomebind()
        {
            return homebind;
        }

        public WorldLocation GetRecall()
        {
            return m_recall_location;
        }

        public void SetRestState(RestTypes type, PlayerRestState state)
        {
            RestInfo restInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.RestInfo, (int)type);
            SetUpdateFieldValue(restInfo.ModifyValue(restInfo.StateID), (byte)state);
        }

        public void SetRestThreshold(RestTypes type, int threshold)
        {
            RestInfo restInfo = m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.RestInfo, (int)type);
            SetUpdateFieldValue(restInfo.ModifyValue(restInfo.Threshold), threshold);
        }

        public bool HasPlayerFlag(PlayerFlags flags) { return m_playerData.PlayerFlags.GetValue().HasAnyFlag(flags); }
        public void SetPlayerFlag(PlayerFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlags), flags); }
        public void RemovePlayerFlag(PlayerFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlags), flags); }
        public void ReplaceAllPlayerFlags(PlayerFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlags), flags); }

        public bool HasPlayerFlagEx(PlayerFlagsEx flags) { return m_playerData.PlayerFlagsEx.GetValue().HasAnyFlag(flags); }
        public void SetPlayerFlagEx(PlayerFlagsEx flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlagsEx), flags); }
        public void RemovePlayerFlagEx(PlayerFlagsEx flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlagsEx), flags); }
        public void ReplaceAllPlayerFlagsEx(PlayerFlagsEx flags) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PlayerFlagsEx), flags); }

        public void SetAverageItemLevelTotal(float newItemLevel) { SetUpdateFieldValue(ref m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.AvgItemLevel, 0), newItemLevel); }
        public void SetAverageItemLevelEquipped(float newItemLevel) { SetUpdateFieldValue(ref m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.AvgItemLevel, 1), newItemLevel); }

        public int GetCustomizationChoice(int chrCustomizationOptionId)
        {
            int choiceIndex = m_playerData.Customizations.FindIndexIf(choice =>
            {
                return choice.ChrCustomizationOptionID == chrCustomizationOptionId;
            });

            if (choiceIndex >= 0)
                return m_playerData.Customizations[choiceIndex].ChrCustomizationChoiceID;

            return 0;
        }

        public void SetCustomizations(List<ChrCustomizationChoice> customizations, bool markChanged = true)
        {
            if (markChanged)
                m_customizationsChanged = true;

            ClearDynamicUpdateFieldValues(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.Customizations));
            foreach (var customization in customizations)
            {
                ChrCustomizationChoice newChoice = new();
                newChoice.ChrCustomizationOptionID = customization.ChrCustomizationOptionID;
                newChoice.ChrCustomizationChoiceID = customization.ChrCustomizationChoiceID;
                AddDynamicUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.Customizations), newChoice);
            }
        }

        public override Gender GetNativeGender() { return (Gender)(byte)m_playerData.NativeSex; }
        public override void SetNativeGender(Gender sex) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.NativeSex), (byte)sex); }
        public void SetPvpTitle(byte pvpTitle) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.PvpTitle), pvpTitle); }
        public void SetArenaFaction(byte arenaFaction) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.ArenaFaction), arenaFaction); }
        public void ApplyModFakeInebriation(int mod, bool apply) { ApplyModUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.FakeInebriation), mod, apply); }
        public void SetVirtualPlayerRealm(uint virtualRealmAddress) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.VirtualPlayerRealm), virtualRealmAddress); }
        public void SetCurrentBattlePetBreedQuality(byte battlePetBreedQuality) { SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.CurrentBattlePetBreedQuality), battlePetBreedQuality); }

        public void AddHeirloom(int itemId, uint flags)
        {
            AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Heirlooms), itemId);
            AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.HeirloomFlags), flags);
        }

        public void SetHeirloom(int slot, int itemId) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Heirlooms, slot), itemId); }
        public void SetHeirloomFlags(int slot, HeirloomPlayerFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.HeirloomFlags, slot), (uint)flags); }

        public void AddToy(int itemId, ToyFlags flags)
        {
            AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Toys), itemId);
        }

        public void AddTransmogBlock(int blockValue) { AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Transmog), blockValue); }
        public void AddTransmogFlag(int slot, uint flag) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.Transmog, slot), (int)flag); }

        public void AddConditionalTransmog(int itemModifiedAppearanceId) { AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ConditionalTransmog), itemModifiedAppearanceId); }
        public void RemoveConditionalTransmog(int itemModifiedAppearanceId)
        {
            int index = m_activePlayerData.ConditionalTransmog.FindIndex(itemModifiedAppearanceId);
            if (index >= 0)
                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ConditionalTransmog), index);
        }

        public void AddSelfResSpell(int spellId) { AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SelfResSpells), spellId); }
        public void RemoveSelfResSpell(int spellId)
        {
            int index = m_activePlayerData.SelfResSpells.FindIndex(spellId);
            if (index >= 0)
                RemoveDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SelfResSpells), index);
        }
        public void ClearSelfResSpell() { ClearDynamicUpdateFieldValues(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.SelfResSpells)); }

        public void SetTrackCreatureFlag(uint flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TrackCreatureMask), flags); }
        public void RemoveTrackCreatureFlag(uint flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TrackCreatureMask), flags); }

        public void SetVersatilityBonus(float value) { SetUpdateFieldStatValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.VersatilityBonus), value); }

        public void ApplyModOverrideSpellPowerByAPPercent(float mod, bool apply) { ApplyModUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OverrideSpellPowerByAPPercent), mod, apply); }

        public void ApplyModOverrideAPBySpellPowerPercent(float mod, bool apply) { ApplyModUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.OverrideAPBySpellPowerPercent), mod, apply); }

        public bool HasPlayerLocalFlag(PlayerLocalFlags flags) { return (m_activePlayerData.LocalFlags & (int)flags) != 0; }
        public void SetPlayerLocalFlag(PlayerLocalFlags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LocalFlags), (uint)flags); }
        public void RemovePlayerLocalFlag(PlayerLocalFlags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LocalFlags), (uint)flags); }
        public void ReplaceAllPlayerLocalFlags(PlayerLocalFlags flags) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LocalFlags), (uint)flags); }

        public byte GetNumRespecs() { return m_activePlayerData.NumRespecs; }
        public void SetNumRespecs(byte numRespecs) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.NumRespecs), numRespecs); }

        public void SetWatchedFactionIndex(int index) { SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.WatchedFactionIndex), index); }

        public void AddAuraVision(PlayerFieldByte2Flags flags) { SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.AuraVision), (byte)flags); }
        public void RemoveAuraVision(PlayerFieldByte2Flags flags) { RemoveUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.AuraVision), (byte)flags); }

        public bool CanTameExoticPets() { return IsGameMaster() || HasAuraType(AuraType.AllowTamePetType); }

        public void SendAttackSwingCancelAttack()
        {
            SendPacket(new CancelCombat());
        }

        public void SetAttackSwingError(AttackSwingErr? err)
        {
            if (err.HasValue && err.Value != m_swingErrorMsg)
                SendPacket(new AttackSwingError(err.Value));

            m_swingErrorMsg = err;
        }

        public void SendAutoRepeatCancel(Unit target)
        {
            CancelAutoRepeat cancelAutoRepeat = new();
            cancelAutoRepeat.Guid = target.GetGUID();                     // may be it's target guid
            SendMessageToSet(cancelAutoRepeat, true);
        }

        public override void BuildCreateUpdateBlockForPlayer(UpdateData data, Player target)
        {
            if (target == this)
            {
                for (byte i = EquipmentSlot.Start; i < InventorySlots.BankBagEnd; ++i)
                {
                    if (m_items[i] == null)
                        continue;

                    m_items[i].BuildCreateUpdateBlockForPlayer(data, target);
                }

                for (byte i = InventorySlots.KeyringStart; i < InventorySlots.ChildEquipmentEnd; ++i)
                {
                    if (m_items[i] == null)
                        continue;

                    m_items[i].BuildCreateUpdateBlockForPlayer(data, target);
                }                
            }

            base.BuildCreateUpdateBlockForPlayer(data, target);
        }

        public override UpdateFieldFlag GetUpdateFieldFlagsFor(Player target)
        {
            UpdateFieldFlag flags = base.GetUpdateFieldFlagsFor(target);
            if (IsInSameRaidWith(target))
                flags |= UpdateFieldFlag.PartyMember;

            return flags;
        }

        public override void BuildValuesCreate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            buffer.WriteUInt8((byte)flags);
            m_objectData.WriteCreate(buffer, flags, this, target);
            m_unitData.WriteCreate(buffer, flags, this, target);
            m_playerData.WriteCreate(buffer, flags, this, target);
            if (target == this)
                m_activePlayerData.WriteCreate(buffer, flags, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdate(WorldPacket data, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            WorldPacket buffer = new();

            buffer.WriteUInt32((uint)(m_values.GetChangedObjectTypeMask() & ~((target != this ? 1 : 0) << (int)TypeId.ActivePlayer)));
            if (m_values.HasChanged(TypeId.Object))
                m_objectData.WriteUpdate(buffer, flags, this, target);

            if (m_values.HasChanged(TypeId.Unit))
                m_unitData.WriteUpdate(buffer, flags, this, target);

            if (m_values.HasChanged(TypeId.Player))
                m_playerData.WriteUpdate(buffer, flags, this, target);

            if (target == this && m_values.HasChanged(TypeId.ActivePlayer))
                m_activePlayerData.WriteUpdate(buffer, flags, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteBytes(buffer);
        }

        public override void BuildValuesUpdateWithFlag(WorldPacket data, UpdateFieldFlag flags, Player target)
        {
            UpdateMask valuesMask = new((int)TypeId.Max);
            valuesMask.Set((int)TypeId.Unit);
            valuesMask.Set((int)TypeId.Player);

            WorldPacket buffer = new();

            UpdateMask mask = m_unitData.GetStaticUpdateMask();
            m_unitData.AppendAllowedFieldsMaskForFlag(mask, flags);
            m_unitData.WriteUpdate(buffer, mask, true, this, target);

            UpdateMask mask2 = m_playerData.GetStaticUpdateMask();
            m_playerData.AppendAllowedFieldsMaskForFlag(mask2, flags);
            m_playerData.WriteUpdate(buffer, mask2, true, this, target);

            data.WriteUInt32(buffer.GetSize());
            data.WriteUInt32(valuesMask.GetBlock(0));
            data.WriteBytes(buffer);
        }

        void BuildValuesUpdateForPlayerWithMask(UpdateData data, UpdateMask requestedObjectMask, UpdateMask requestedUnitMask, UpdateMask requestedPlayerMask, UpdateMask requestedActivePlayerMask, Player target)
        {
            UpdateFieldFlag flags = GetUpdateFieldFlagsFor(target);
            UpdateMask valuesMask = new((int)TypeId.Max);
            if (requestedObjectMask.IsAnySet())
                valuesMask.Set((int)TypeId.Object);

            m_unitData.FilterDisallowedFieldsMaskForFlag(requestedUnitMask, flags);
            if (requestedUnitMask.IsAnySet())
                valuesMask.Set((int)TypeId.Unit);

            m_playerData.FilterDisallowedFieldsMaskForFlag(requestedPlayerMask, flags);
            if (requestedPlayerMask.IsAnySet())
                valuesMask.Set((int)TypeId.Player);

            if (target == this && requestedActivePlayerMask.IsAnySet())
                valuesMask.Set((int)TypeId.ActivePlayer);

            WorldPacket buffer = new();
            buffer.WriteUInt32(valuesMask.GetBlock(0));

            if (valuesMask[(int)TypeId.Object])
                m_objectData.WriteUpdate(buffer, requestedObjectMask, true, this, target);

            if (valuesMask[(int)TypeId.Unit])
                m_unitData.WriteUpdate(buffer, requestedUnitMask, true, this, target);

            if (valuesMask[(int)TypeId.Player])
                m_playerData.WriteUpdate(buffer, requestedPlayerMask, true, this, target);

            if (valuesMask[(int)TypeId.ActivePlayer])
                m_activePlayerData.WriteUpdate(buffer, requestedActivePlayerMask, true, this, target);

            WorldPacket buffer1 = new();
            buffer1.WriteUInt8((byte)UpdateType.Values);
            buffer1.WritePackedGuid(GetGUID());
            buffer1.WriteUInt32(buffer.GetSize());
            buffer1.WriteBytes(buffer.GetData());

            data.AddUpdateBlock(buffer1);
        }

        public override void ClearUpdateMask(bool remove)
        {
            m_values.ClearChangesMask(m_playerData);
            m_values.ClearChangesMask(m_activePlayerData);
            base.ClearUpdateMask(remove);
        }

        //Helpers
        public void AddGossipItem(GossipOptionNpc optionNpc, string text, int sender, int action)
        {
            PlayerTalkClass.GetGossipMenu().AddMenuItem(0, -1, optionNpc, text, 0, GossipOptionFlags.None, null, 0, 0, false, 0, "", null, null, sender, action);
        }

        public void AddGossipItem(GossipOptionNpc optionNpc, string text, int sender, int action, string popupText, uint popupMoney, bool coded)
        {
            PlayerTalkClass.GetGossipMenu().AddMenuItem(0, -1, optionNpc, text, 0, GossipOptionFlags.None, null, 0, 0, coded, popupMoney, popupText, null, null, sender, action);
        }

        public void AddGossipItem(int gossipMenuID, int gossipMenuItemID, int sender, int action)
        {
            PlayerTalkClass.GetGossipMenu().AddMenuItem(gossipMenuID, gossipMenuItemID, sender, action);
        }

        // This fuction Sends the current menu to show to client, a - NPCTEXTID(uint32), b - npc guid(uint64)
        public void SendGossipMenu(int titleId, ObjectGuid objGUID) { PlayerTalkClass.SendGossipMenu(titleId, objGUID); }

        // Closes the Menu
        public void CloseGossipMenu() { PlayerTalkClass.SendCloseGossip(); }

        public void InitGossipMenu(int menuId) { PlayerTalkClass.GetGossipMenu().SetMenuId(menuId); }

        //Clears the Menu
        public void ClearGossipMenu() { PlayerTalkClass.ClearMenus(); }
    }
}