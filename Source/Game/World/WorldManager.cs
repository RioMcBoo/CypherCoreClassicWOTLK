// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework;
using Framework.Collections;
using Framework.Configuration;
using Framework.Constants;
using Framework.Database;
using Framework.Realm;
using Game.BattlePets;
using Game.Chat;
using Game.Collision;
using Game.DataStorage;
using Game.Entities;
using Game.Maps;
using Game.Networking;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public class WorldManager : Singleton<WorldManager>
    {
        public const string NextCurrencyResetTimeVarId = "NextCurrencyResetTime";
        public const string NextWeeklyQuestResetTimeVarId = "NextWeeklyQuestResetTime";
        public const string NextBGRandomDailyResetTimeVarId = "NextBGRandomDailyResetTime";
        public const string CharacterDatabaseCleaningFlagsVarId = "PersistentCharacterCleanFlags";
        public const string NextGuildDailyResetTimeVarId = "NextGuildDailyResetTime";
        public const string NextMonthlyQuestResetTimeVarId = "NextMonthlyQuestResetTime";
        public const string NextDailyQuestResetTimeVarId = "NextDailyQuestResetTime";
        public const string NextOldCalendarEventDeletionTimeVarId = "NextOldCalendarEventDeletionTime";
        public const string NextGuildWeeklyResetTimeVarId = "NextGuildWeeklyResetTime";

        WorldManager()
        {
            foreach (WorldTimers timer in Enum.GetValues(typeof(WorldTimers)))
                m_timers[timer] = new IntervalTimer();

            m_allowedSecurityLevel = AccountTypes.Player;

            _realm = new Realm();

            _worldUpdateTime = new WorldUpdateTime();
            _warnShutdownTime = Time.Now.ToServerTime();
        }

        public Player FindPlayerInZone(int zone)
        {
            foreach (var session in m_sessions)
            {
                Player player = session.Value.GetPlayer();
                if (player == null)
                    continue;

                if (player.IsInWorld && player.GetZoneId() == zone)
                {
                    // Used by the weather system. We return the player to broadcast the change weather message to him and all players in the zone.
                    return player;
                }
            }
            return null;
        }

        public bool IsClosed()
        {
            return m_isClosed;
        }

        public void SetClosed(bool val)
        {
            m_isClosed = val;
            Global.ScriptMgr.OnOpenStateChange(!val);
        }

        public void LoadDBAllowedSecurityLevel()
        {
            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_REALMLIST_SECURITY_LEVEL);
            stmt.SetInt32(0, _realm.Id.Index);
            SQLResult result = DB.Login.Query(stmt);

            if (!result.IsEmpty())
                SetPlayerSecurityLimit((AccountTypes)result.Read<byte>(0));
        }

        public void SetPlayerSecurityLimit(AccountTypes _sec)
        {
            AccountTypes sec = _sec < AccountTypes.Console ? _sec : AccountTypes.Player;
            bool update = sec > m_allowedSecurityLevel;
            m_allowedSecurityLevel = sec;
            if (update)
                KickAllLess(m_allowedSecurityLevel);
        }

        public void SetMotd(string motd)
        {
            Global.ScriptMgr.OnMotdChange(motd);

            m_motd.Clear();
            m_motd.AddRange(motd.Split('@'));
        }

        public List<string> GetMotd()
        {
            return m_motd;
        }

        public void TriggerGuidWarning()
        {
            // Lock this only to prevent multiple maps triggering at the same time
            lock (_guidAlertLock)
            {
                ServerTime now = LoopTime.ServerTime;
                ServerTime today = now.Date;

                // Check if our window to restart today has passed. 5 mins until quiet time
                while (now >= GetHourTimestamp(today, WorldConfig.Values[WorldCfg.RespawnRestartQuietTime].Int32) - (Seconds)1810)
                    today += (Days)1;

                // Schedule restart for 30 minutes before quiet time, or as long as we have
                _warnShutdownTime = (ServerTime)GetHourTimestamp(today, WorldConfig.Values[WorldCfg.RespawnRestartQuietTime].Int32) - (Seconds)1800;

                _guidWarn = true;
                SendGuidWarning();
            }
        }

        public void TriggerGuidAlert()
        {
            // Lock this only to prevent multiple maps triggering at the same time
            lock (_guidAlertLock)
            {
                DoGuidAlertRestart();
                _guidAlert = true;
                _guidWarn = false;
            }
        }

        void DoGuidWarningRestart()
        {
            if (IsShuttingDown)
                return;

            ShutdownServ((Seconds)1800, ShutdownMask.Restart, ShutdownExitCode.Restart);
            _warnShutdownTime += Time.Hour;
        }

        void DoGuidAlertRestart()
        {
            if (IsShuttingDown)
                return;

            ShutdownServ((Seconds)300, ShutdownMask.Restart, ShutdownExitCode.Restart, _alertRestartReason);
        }

        void SendGuidWarning()
        {
            if (!IsShuttingDown && _guidWarn && WorldConfig.Values[WorldCfg.RespawnGuidWarningFrequency].TimeSpan > TimeSpan.Zero)
                SendServerMessage(ServerMessageType.String, _guidWarningMsg);
            _warnDiff = TimeSpan.Zero;
        }

        public WorldSession FindSession(int id)
        {
            return m_sessions.LookupByKey(id);
        }

        bool RemoveSession(int id)
        {
            // Find the session, kick the user, but we can't delete session at this moment to prevent iterator invalidation
            var session = m_sessions.LookupByKey(id);

            if (session != null)
            {
                if (session.PlayerLoading())
                    return false;

                session.KickPlayer("World::RemoveSession");
            }

            return true;
        }

        public void AddSession(WorldSession s)
        {
            addSessQueue.Enqueue(s);
        }

        public void AddInstanceSocket(WorldSocket sock, long connectToKey)
        {
            _linkSocketQueue.Enqueue(Tuple.Create(sock, connectToKey));
        }

        void AddSession_(WorldSession s)
        {
            Cypher.Assert(s != null);

            //NOTE - Still there is race condition in WorldSession* being used in the Sockets

            // kick already loaded player with same account (if any) and remove session
            // if player is in loading and want to load again, return
            if (!RemoveSession(s.GetAccountId()))
            {
                s.KickPlayer("World::AddSession_ Couldn't remove the other session while on loading screen");
                return;
            }

            // decrease session counts only at not reconnection case
            bool decrease_session = true;

            // if session already exist, prepare to it deleting at next world update
            // NOTE - KickPlayer() should be called on "old" in RemoveSession()
            {
                var old = m_sessions.LookupByKey(s.GetAccountId());
                if (old != null)
                {
                    // prevent decrease sessions count if session queued
                    if (RemoveQueuedPlayer(old))
                        decrease_session = false;

                    m_sessionsByBnetGuid.Remove(old.GetBattlenetAccountGUID(), old);
                    old.Dispose();
                }
            }

            m_sessions[s.GetAccountId()] = s;
            m_sessionsByBnetGuid.Add(s.GetBattlenetAccountGUID(), s);

            int Sessions = GetActiveAndQueuedSessionCount();
            int pLimit = GetPlayerAmountLimit();
            int QueueSize = GetQueuedSessionCount(); //number of players in the queue

            //so we don't count the user trying to
            //login as a session and queue the socket that we are using
            if (decrease_session)
                --Sessions;

            if (pLimit > 0 && Sessions >= pLimit && !s.HasPermission(RBACPermissions.SkipQueue) && !HasRecentlyDisconnected(s))
            {
                AddQueuedPlayer(s);
                UpdateMaxSessionCounters();
                Log.outInfo(LogFilter.Server, 
                    $"PlayerQueue: Account id {s.GetAccountId()} is in Queue Position ({++QueueSize}).");
                return;
            }

            s.InitializeSession();

            UpdateMaxSessionCounters();

            // Updates the population
            if (pLimit > 0)
            {
                float popu = GetActiveSessionCount();              // updated number of users on the server
                popu /= pLimit;
                popu *= 2;
                Log.outInfo(LogFilter.Server, $"Server Population ({popu}).");
            }
        }

        void ProcessLinkInstanceSocket(Tuple<WorldSocket, long> linkInfo)
        {
            if (!linkInfo.Item1.IsOpen())
                return;

            ConnectToKey key = new();
            key.Raw = linkInfo.Item2;

            WorldSession session = FindSession(key.AccountId);
            if (session == null || session.GetConnectToInstanceKey() != linkInfo.Item2)
            {
                linkInfo.Item1.SendAuthResponseError(BattlenetRpcErrorCode.TimedOut);
                linkInfo.Item1.CloseSocket();
                return;
            }

            linkInfo.Item1.SetWorldSession(session);
            session.AddInstanceConnection(linkInfo.Item1);
            session.HandleContinuePlayerLogin();
        }

        bool HasRecentlyDisconnected(WorldSession session)
        {
            if (session == null)
                return false;

            TimeSpan tolerance = TimeSpan.Zero;

            if (tolerance != TimeSpan.Zero)
            {
                foreach (var disconnect in m_disconnects)
                {
                    if ((disconnect.Value - LoopTime.ServerTime) < tolerance)
                    {
                        if (disconnect.Key == session.GetAccountId())
                            return true;
                    }
                    else
                        m_disconnects.Remove(disconnect.Key);
                }
            }
            return false;
        }

        uint GetQueuePos(WorldSession sess)
        {
            uint position = 1;

            foreach (var iter in m_QueuedPlayer)
            {
                if (iter != sess)
                    ++position;
                else
                    return position;
            }
            return 0;
        }

        void AddQueuedPlayer(WorldSession sess)
        {
            sess.SetInQueue(true);
            m_QueuedPlayer.Add(sess);

            // The 1st SMSG_AUTH_RESPONSE needs to contain other info too.
            sess.SendAuthResponse(BattlenetRpcErrorCode.Ok, true, GetQueuePos(sess));
        }

        bool RemoveQueuedPlayer(WorldSession sess)
        {
            // sessions count including queued to remove (if removed_session set)
            int sessions = GetActiveSessionCount();

            uint position = 1;

            // search to remove and count skipped positions
            bool found = false;

            foreach (var iter in m_QueuedPlayer)
            {
                if (iter != sess)
                    ++position;
                else
                {
                    sess.SetInQueue(false);
                    sess.ResetTimeOutTime(false);
                    m_QueuedPlayer.Remove(iter);
                    found = true;                                   // removing queued session
                    break;
                }
            }

            // iter point to next socked after removed or end()
            // position store position of removed socket and then new position next socket after removed

            // if session not queued then we need decrease sessions count
            if (!found && sessions != 0)
                --sessions;

            // accept first in queue
            if ((m_playerLimit == 0 || sessions < m_playerLimit) && !m_QueuedPlayer.Empty())
            {
                WorldSession pop_sess = m_QueuedPlayer.First();
                pop_sess.InitializeSession();

                m_QueuedPlayer.RemoveAt(0);

                // update iter to point first queued socket or end() if queue is empty now
                position = 1;
            }

            // update position from iter to end()
            // iter point to first not updated socket, position store new position
            foreach (var iter in m_QueuedPlayer)
            {
                iter.SendAuthWaitQueue(++position);
            }

            return found;
        }

        public void SetInitialWorldSettings()
        {
            _realm = Global.RealmMgr.GetRealms().First();
            Log.SetRealmId(_realm.Id.Index);

            LoadConfigSettings();

            // Initialize Allowed Security Level
            LoadDBAllowedSecurityLevel();

            Global.ObjectMgr.SetHighestGuids();

            if (!TerrainManager.ExistMapAndVMap(0, -6240.32f, 331.033f) || !TerrainManager.ExistMapAndVMap(0, -8949.95f, -132.493f)
                || !TerrainManager.ExistMapAndVMap(1, -618.518f, -4251.67f) || !TerrainManager.ExistMapAndVMap(0, 1676.35f, 1677.45f)
                || !TerrainManager.ExistMapAndVMap(1, 10311.3f, 832.463f) || !TerrainManager.ExistMapAndVMap(1, -2917.58f, -257.98f)
                || (WorldConfig.Values[WorldCfg.Expansion].Int32 != 0 && (!TerrainManager.ExistMapAndVMap(530, 10349.6f, -6357.29f) || !TerrainManager.ExistMapAndVMap(530, -3961.64f, -13931.2f))))
            {
                Log.outError(LogFilter.ServerLoading, "Unable to load map and vmap data for starting zones - server shutting down!");
                Environment.Exit(1);
            }

            // Initialize pool manager
            Global.PoolMgr.Initialize();

            // Initialize game event manager
            Global.GameEventMgr.Initialize();

            Log.outInfo(LogFilter.ServerLoading, "Loading Cypher Strings...");
            if (!Global.ObjectMgr.LoadCypherStrings())
                Environment.Exit(1);

            // not send custom Type REALM_FFA_PVP to realm list
            RealmType server_type = IsFFAPvPRealm() ? RealmType.PVP : (RealmType)WorldConfig.Values[WorldCfg.GameType].Int32;
            int realm_zone = WorldConfig.Values[WorldCfg.RealmZone].Int32;

            DB.Login.Execute($"UPDATE realmlist SET icon = {(int)server_type}, timezone = {realm_zone} WHERE id = '{_realm.Id.Index}'");      // One-time query

            Log.outInfo(LogFilter.ServerLoading, "Initialize DataStorage...");
            // Load DB2s
            m_availableDbcLocaleMask = CliDB.LoadStores(_dataPath, m_defaultDbcLocale);

            if (m_availableDbcLocaleMask == null || !m_availableDbcLocaleMask[(int)m_defaultDbcLocale])
            {
                Log.outFatal(LogFilter.ServerLoading, 
                    $"Unable to load db2 files for {m_defaultDbcLocale} locale specified in DBC.Locale config!");

                Environment.Exit(1);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObject models...");
            if (!GameObjectModel.LoadGameObjectModelList())
            {
                Log.outFatal(LogFilter.ServerLoading, 
                    "Unable to load gameobject models (part of vmaps), " +
                    "objects using WMO models will crash the client - server shutting down!");

                Environment.Exit(1);
            }

            Log.outInfo(LogFilter.ServerLoading, "Loading hotfix blobs...");
            Global.DB2Mgr.LoadHotfixBlob(m_availableDbcLocaleMask);

            Log.outInfo(LogFilter.ServerLoading, "Loading hotfix info...");
            Global.DB2Mgr.LoadHotfixData(m_availableDbcLocaleMask);

            Log.outInfo(LogFilter.ServerLoading, "Loading hotfix optional data...");
            Global.DB2Mgr.LoadHotfixOptionalData(m_availableDbcLocaleMask);

            //- Load M2 fly by cameras
            M2Storage.LoadM2Cameras(_dataPath);

            //- Load GameTables
            CliDB.LoadGameTables(_dataPath);

            //Load weighted graph on taxi nodes path
            TaxiPathGraph.Initialize();

            // always use declined names in the russian client
            var category = CliDB.CfgCategoriesStorage.LookupByKey(WorldConfig.Values[WorldCfg.RealmZone].Int32);
            if (category != null && category.CreateCharsetMask.HasFlag(CfgCategoriesCharsets.Russian))
                WorldConfig.SetValue(WorldCfg.DeclinedNamesUsed, true);

            MultiMap<int, int> mapData = new();
            foreach (MapRecord mapEntry in CliDB.MapStorage.Values)
            {
                if (mapEntry.ParentMapID != -1)
                {
                    Cypher.Assert(mapEntry.CosmeticParentMapID == -1 || mapEntry.ParentMapID == mapEntry.CosmeticParentMapID, 
                        $"Inconsistent parent map data for map {mapEntry.Id} (ParentMapID = {mapEntry.ParentMapID}, " +
                        $"CosmeticParentMapID = {mapEntry.CosmeticParentMapID})");

                    mapData.Add(mapEntry.ParentMapID, mapEntry.Id);
                }
                else if (mapEntry.CosmeticParentMapID != -1)
                    mapData.Add(mapEntry.CosmeticParentMapID, mapEntry.Id);
            }

            Global.TerrainMgr.InitializeParentMapData(mapData);

            Global.VMapMgr.Initialize(mapData);
            Global.MMapMgr.Initialize(mapData);

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo Storage...");
            Global.SpellMgr.LoadSpellInfoStore();

            Log.outInfo(LogFilter.ServerLoading, "Loading serverside spells...");
            Global.SpellMgr.LoadSpellInfoServerside();

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo corrections...");
            Global.SpellMgr.LoadSpellInfoCorrections();

            Log.outInfo(LogFilter.ServerLoading, "Loading SkillLineAbility Data...");
            Global.SpellMgr.LoadSkillLineAbilityMap();

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo custom attributes...");
            Global.SpellMgr.LoadSpellInfoCustomAttributes();

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo diminishing infos...");
            Global.SpellMgr.LoadSpellInfoDiminishing();

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo immunity infos...");
            Global.SpellMgr.LoadSpellInfoImmunities();

            Log.outInfo(LogFilter.ServerLoading, "Loading PetFamilySpellsStore Data...");
            Global.SpellMgr.LoadPetFamilySpellsStore();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Totem models...");
            Global.SpellMgr.LoadSpellTotemModel();

            Log.outInfo(LogFilter.ServerLoading, "Loading Traits...");
            TraitMgr.Load();

            Log.outInfo(LogFilter.ServerLoading, "Loading languages...");
            Global.LanguageMgr.LoadLanguages();

            Log.outInfo(LogFilter.ServerLoading, "Loading languages words...");
            Global.LanguageMgr.LoadLanguagesWords();

            Log.outInfo(LogFilter.ServerLoading, "Loading Instance Template...");
            Global.ObjectMgr.LoadInstanceTemplate();

            // Must be called before `respawn` data
            Log.outInfo(LogFilter.ServerLoading, "Loading instances...");

            Global.MapMgr.InitInstanceIds();
            Global.InstanceLockMgr.Load();

            Log.outInfo(LogFilter.ServerLoading, "Loading Localization strings...");
            RelativeTime oldMSTime = Time.NowRelative;
            Global.ObjectMgr.LoadCreatureLocales();
            Global.ObjectMgr.LoadGameObjectLocales();
            Global.ObjectMgr.LoadQuestTemplateLocale();
            Global.ObjectMgr.LoadQuestOfferRewardLocale();
            Global.ObjectMgr.LoadQuestRequestItemsLocale();
            Global.ObjectMgr.LoadQuestObjectivesLocale();
            Global.ObjectMgr.LoadPageTextLocales();
            Global.ObjectMgr.LoadGossipMenuItemsLocales();
            Global.ObjectMgr.LoadPointOfInterestLocales();

            Log.outInfo(LogFilter.ServerLoading, $"Localization strings loaded in {Time.Diff(oldMSTime)} ms.");

            Log.outInfo(LogFilter.ServerLoading, "Loading Account Roles and Permissions...");
            Global.AccountMgr.LoadRBAC();

            Log.outInfo(LogFilter.ServerLoading, "Loading Page Texts...");
            Global.ObjectMgr.LoadPageTexts();

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObject Template...");
            Global.ObjectMgr.LoadGameObjectTemplate();

            Log.outInfo(LogFilter.ServerLoading, "Loading Game Object template addons...");
            Global.ObjectMgr.LoadGameObjectTemplateAddons();

            Log.outInfo(LogFilter.ServerLoading, "Loading Transport Templates...");
            Global.TransportMgr.LoadTransportTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Transport animations and rotations...");
            Global.TransportMgr.LoadTransportAnimationAndRotation();

            Log.outInfo(LogFilter.ServerLoading, "Loading Transport spawns...");
            Global.TransportMgr.LoadTransportSpawns();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Rank Data...");
            Global.SpellMgr.LoadSpellRanks();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Required Data...");
            Global.SpellMgr.LoadSpellRequired();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Group types...");
            Global.SpellMgr.LoadSpellGroups();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Learn Skills...");
            Global.SpellMgr.LoadSpellLearnSkills();

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellInfo SpellSpecific and AuraState...");
            Global.SpellMgr.LoadSpellInfoSpellSpecificAndAuraState();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Learn Spells...");
            Global.SpellMgr.LoadSpellLearnSpells();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Proc conditions and data...");
            Global.SpellMgr.LoadSpellProcs();

            Log.outInfo(LogFilter.ServerLoading, "Loading Aggro Spells Definitions...");
            Global.SpellMgr.LoadSpellThreats();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell Group Stack Rules...");
            Global.SpellMgr.LoadSpellGroupStackRules();

            Log.outInfo(LogFilter.ServerLoading, "Loading NPC Texts...");
            Global.ObjectMgr.LoadNPCText();

            Log.outInfo(LogFilter.ServerLoading, "Loading Enchant Spells Proc datas...");
            Global.SpellMgr.LoadSpellEnchantProcData();

            Log.outInfo(LogFilter.ServerLoading, "Loading Disables");                         // must be before loading quests and items
            Global.DisableMgr.LoadDisables();

            Log.outInfo(LogFilter.ServerLoading, "Loading Item Random Enchantments Table...");
            ItemEnchantmentManager.LoadRandomEnchantmentsTable();

            Log.outInfo(LogFilter.ServerLoading, "Loading Items...");                         // must be after LoadRandomEnchantmentsTable and LoadPageTexts
            Global.ObjectMgr.LoadItemTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Item set names...");                // must be after LoadItemPrototypes
            Global.ObjectMgr.LoadItemTemplateAddon();

            Log.outInfo(LogFilter.ServerLoading, "Loading Item Scripts...");                 // must be after LoadItemPrototypes
            Global.ObjectMgr.LoadItemScriptNames();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Model Based Info Data...");
            Global.ObjectMgr.LoadCreatureModelInfo();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature templates...");
            Global.ObjectMgr.LoadCreatureTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Equipment templates...");
            Global.ObjectMgr.LoadEquipmentTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature template addons...");
            Global.ObjectMgr.LoadCreatureTemplateAddons();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature template difficulty...");
            Global.ObjectMgr.LoadCreatureTemplateDifficulty();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature template sparring...");
            Global.ObjectMgr.LoadCreatureTemplateSparring();

            Log.outInfo(LogFilter.ServerLoading, "Loading Reputation Reward Rates...");
            Global.ObjectMgr.LoadReputationRewardRate();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Reputation OnKill Data...");
            Global.ObjectMgr.LoadReputationOnKill();

            Log.outInfo(LogFilter.ServerLoading, "Loading Reputation Spillover Data...");
            Global.ObjectMgr.LoadReputationSpilloverTemplate();

            Log.outInfo(LogFilter.ServerLoading, "Loading Points Of Interest Data...");
            Global.ObjectMgr.LoadPointsOfInterest();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Base Stats...");
            Global.ObjectMgr.LoadCreatureClassLevelStats();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spawn Group Templates...");
            Global.ObjectMgr.LoadSpawnGroupTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Data...");
            Global.ObjectMgr.LoadCreatures();

            Log.outInfo(LogFilter.ServerLoading, "Loading Temporary Summon Data...");
            Global.ObjectMgr.LoadTempSummons();                               // must be after LoadCreatureTemplates() and LoadGameObjectTemplates()

            Log.outInfo(LogFilter.ServerLoading, "Loading pet levelup spells...");
            Global.SpellMgr.LoadPetLevelupSpellMap();

            Log.outInfo(LogFilter.ServerLoading, "Loading pet default spells additional to levelup spells...");
            Global.SpellMgr.LoadPetDefaultSpells();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Addon Data...");
            Global.ObjectMgr.LoadCreatureAddons();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Movement Overrides...");
            Global.ObjectMgr.LoadCreatureMovementOverrides();                 // must be after LoadCreatures()

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObjects...");
            Global.ObjectMgr.LoadGameObjects();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spawn Group Data...");
            Global.ObjectMgr.LoadSpawnGroups();

            Log.outInfo(LogFilter.ServerLoading, "Loading instance spawn groups...");
            Global.ObjectMgr.LoadInstanceSpawnGroups();

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObject Addon Data...");
            Global.ObjectMgr.LoadGameObjectAddons();                          // must be after LoadGameObjects()

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObject faction and flags overrides...");
            Global.ObjectMgr.LoadGameObjectOverrides();                       // must be after LoadGameObjects()

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObject Quest Items...");
            Global.ObjectMgr.LoadGameObjectQuestItems();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Quest Items...");
            Global.ObjectMgr.LoadCreatureQuestItems();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Linked Respawn...");
            Global.ObjectMgr.LoadLinkedRespawn();                             // must be after LoadCreatures(), LoadGameObjects()

            Log.outInfo(LogFilter.ServerLoading, "Loading Weather Data...");
            Global.WeatherMgr.LoadWeatherData();

            Log.outInfo(LogFilter.ServerLoading, "Loading Quests...");
            Global.ObjectMgr.LoadQuests();

            Log.outInfo(LogFilter.ServerLoading, "Checking Quest Disables");
            Global.DisableMgr.CheckQuestDisables();                           // must be after loading quests

            Log.outInfo(LogFilter.ServerLoading, "Loading Quest POI");
            Global.ObjectMgr.LoadQuestPOI();

            Log.outInfo(LogFilter.ServerLoading, "Loading Quests Starters and Enders...");
            Global.ObjectMgr.LoadQuestStartersAndEnders();                    // must be after quest load

            Log.outInfo(LogFilter.ServerLoading, "Loading Quest Greetings...");
            Global.ObjectMgr.LoadQuestGreetings();
            Global.ObjectMgr.LoadQuestGreetingLocales();

            Log.outInfo(LogFilter.ServerLoading, "Loading Objects Pooling Data...");
            Global.PoolMgr.LoadFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Loading Quest Pooling Data...");
            Global.QuestPoolMgr.LoadFromDB();                                // must be after quest templates

            Log.outInfo(LogFilter.ServerLoading, "Loading Game Event Data...");               // must be after loading pools fully
            Global.GameEventMgr.LoadFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Loading creature summoned data...");
            Global.ObjectMgr.LoadCreatureSummonedData();                     // must be after LoadCreatureTemplates() and LoadQuests()

            Log.outInfo(LogFilter.ServerLoading, "Loading NPCSpellClick Data..."); // must be after LoadQuests
            Global.ObjectMgr.LoadNPCSpellClickSpells();

            Log.outInfo(LogFilter.ServerLoading, "Loading Vehicle Templates...");
            Global.ObjectMgr.LoadVehicleTemplate();                          // must be after LoadCreatureTemplates()

            Log.outInfo(LogFilter.ServerLoading, "Loading Vehicle Template Accessories...");
            Global.ObjectMgr.LoadVehicleTemplateAccessories();                // must be after LoadCreatureTemplates() and LoadNPCSpellClickSpells()

            Log.outInfo(LogFilter.ServerLoading, "Loading Vehicle Accessories...");
            Global.ObjectMgr.LoadVehicleAccessories();                       // must be after LoadCreatureTemplates() and LoadNPCSpellClickSpells()

            Log.outInfo(LogFilter.ServerLoading, "Loading Vehicle Seat Addon Data...");
            Global.ObjectMgr.LoadVehicleSeatAddon();                         // must be after loading DBC

            Log.outInfo(LogFilter.ServerLoading, "Loading SpellArea Data...");                // must be after quest load
            Global.SpellMgr.LoadSpellAreas();

            Log.outInfo(LogFilter.ServerLoading, "Loading World locations...");
            Global.ObjectMgr.LoadWorldSafeLocs();                            // must be before LoadAreaTriggerTeleports and LoadGraveyardZones

            Log.outInfo(LogFilter.ServerLoading, "Loading Area Trigger Teleports definitions...");
            Global.ObjectMgr.LoadAreaTriggerTeleports();

            Log.outInfo(LogFilter.ServerLoading, "Loading Access Requirements...");
            Global.ObjectMgr.LoadAccessRequirements();                        // must be after item template load

            Log.outInfo(LogFilter.ServerLoading, "Loading Quest Area Triggers...");
            Global.ObjectMgr.LoadQuestAreaTriggers();                         // must be after LoadQuests

            Log.outInfo(LogFilter.ServerLoading, "Loading Tavern Area Triggers...");
            Global.ObjectMgr.LoadTavernAreaTriggers();

            Log.outInfo(LogFilter.ServerLoading, "Loading AreaTrigger script names...");
            Global.ObjectMgr.LoadAreaTriggerScripts();

            Log.outInfo(LogFilter.ServerLoading, "Loading LFG entrance positions..."); // Must be after areatriggers
            Global.LFGMgr.LoadLFGDungeons();

            Log.outInfo(LogFilter.ServerLoading, "Loading LFG rewards...");
            Global.LFGMgr.LoadRewards();

            Log.outInfo(LogFilter.ServerLoading, "Loading Graveyard-zone links...");
            Global.ObjectMgr.LoadGraveyardZones();

            Log.outInfo(LogFilter.ServerLoading, "Loading spell pet auras...");
            Global.SpellMgr.LoadSpellPetAuras();

            Log.outInfo(LogFilter.ServerLoading, "Loading Spell target coordinates...");
            Global.SpellMgr.LoadSpellTargetPositions();

            Log.outInfo(LogFilter.ServerLoading, "Loading linked spells...");
            Global.SpellMgr.LoadSpellLinked();

            Log.outInfo(LogFilter.ServerLoading, "Loading Scenes Templates..."); // must be before LoadPlayerInfo
            Global.ObjectMgr.LoadSceneTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Player Create Data...");
            Global.ObjectMgr.LoadPlayerInfo();

            Log.outInfo(LogFilter.ServerLoading, "Loading Exploration BaseXP Data...");
            Global.ObjectMgr.LoadExplorationBaseXP();

            Log.outInfo(LogFilter.ServerLoading, "Loading Pet Name Parts...");
            Global.ObjectMgr.LoadPetNames();

            Log.outInfo(LogFilter.ServerLoading, "Loading AreaTrigger Templates...");
            Global.AreaTriggerDataStorage.LoadAreaTriggerTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading AreaTrigger Spawns...");
            Global.AreaTriggerDataStorage.LoadAreaTriggerSpawns();

            Log.outInfo(LogFilter.ServerLoading, "Loading Conversation Templates...");
            Global.ConversationDataStorage.LoadConversationTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading Player Choices...");
            Global.ObjectMgr.LoadPlayerChoices();

            Log.outInfo(LogFilter.ServerLoading, "Loading Player Choices Locales...");
            Global.ObjectMgr.LoadPlayerChoicesLocale();

            Log.outInfo(LogFilter.ServerLoading, "Loading Jump Charge Params...");
            Global.ObjectMgr.LoadJumpChargeParams();

            CharacterDatabaseCleaner.CleanDatabase();

            Log.outInfo(LogFilter.ServerLoading, "Loading the max pet number...");
            Global.ObjectMgr.LoadPetNumber();

            Log.outInfo(LogFilter.ServerLoading, "Loading pet level stats...");
            Global.ObjectMgr.LoadPetLevelInfo();

            Log.outInfo(LogFilter.ServerLoading, "Loading Player level dependent mail rewards...");
            Global.ObjectMgr.LoadMailLevelRewards();

            // Loot tables
            Loots.LootManager.LoadLootTables();

            Log.outInfo(LogFilter.ServerLoading, "Loading Skill Discovery Table...");
            SkillDiscovery.LoadSkillDiscoveryTable();

            Log.outInfo(LogFilter.ServerLoading, "Loading Skill Extra Item Table...");
            SkillExtraItems.LoadSkillExtraItemTable();

            Log.outInfo(LogFilter.ServerLoading, "Loading Skill Perfection Data Table...");
            SkillPerfectItems.LoadSkillPerfectItemTable();

            Log.outInfo(LogFilter.ServerLoading, "Loading Skill Fishing base level requirements...");
            Global.ObjectMgr.LoadFishingBaseSkillLevel();

            Log.outInfo(LogFilter.ServerLoading, "Loading skill tier info...");
            Global.ObjectMgr.LoadSkillTiers();

            Log.outInfo(LogFilter.ServerLoading, "Loading Criteria Modifier trees...");
            Global.CriteriaMgr.LoadCriteriaModifiersTree();
            Log.outInfo(LogFilter.ServerLoading, "Loading Criteria Lists...");
            Global.CriteriaMgr.LoadCriteriaList();
            Log.outInfo(LogFilter.ServerLoading, "Loading Criteria Data...");
            Global.CriteriaMgr.LoadCriteriaData();
            Log.outInfo(LogFilter.ServerLoading, "Loading Achievements...");
            Global.AchievementMgr.LoadAchievementReferenceList();
            Log.outInfo(LogFilter.ServerLoading, "Loading Achievements Scripts...");
            Global.AchievementMgr.LoadAchievementScripts();
            Log.outInfo(LogFilter.ServerLoading, "Loading Achievement Rewards...");
            Global.AchievementMgr.LoadRewards();
            Log.outInfo(LogFilter.ServerLoading, "Loading Achievement Reward Locales...");
            Global.AchievementMgr.LoadRewardLocales();
            Log.outInfo(LogFilter.ServerLoading, "Loading Completed Achievements...");
            Global.AchievementMgr.LoadCompletedAchievements();

            // Load before guilds and arena teams
            Log.outInfo(LogFilter.ServerLoading, "Loading character cache store...");
            Global.CharacterCacheStorage.LoadCharacterCacheStorage();

            // Load dynamic data tables from the database
            Log.outInfo(LogFilter.ServerLoading, "Loading Auctions...");
            Global.AuctionHouseMgr.LoadAuctions();

            if (WorldConfig.Values[WorldCfg.BlackmarketEnabled].Bool)
            {
                Log.outInfo(LogFilter.ServerLoading, "Loading Black Market Templates...");
                Global.BlackMarketMgr.LoadTemplates();

                Log.outInfo(LogFilter.ServerLoading, "Loading Black Market Auctions...");
                Global.BlackMarketMgr.LoadAuctions();
            }

            Log.outInfo(LogFilter.ServerLoading, "Loading Guild rewards...");
            Global.GuildMgr.LoadGuildRewards();

            Log.outInfo(LogFilter.ServerLoading, "Loading Guilds...");
            Global.GuildMgr.LoadGuilds();


            Log.outInfo(LogFilter.ServerLoading, "Loading ArenaTeams...");
            Global.ArenaTeamMgr.LoadArenaTeams();

            Log.outInfo(LogFilter.ServerLoading, "Loading Groups...");
            Global.GroupMgr.LoadGroups();

            Log.outInfo(LogFilter.ServerLoading, "Loading ReservedNames...");
            Global.ObjectMgr.LoadReservedPlayersNames();

            Log.outInfo(LogFilter.ServerLoading, "Loading GameObjects for quests...");
            Global.ObjectMgr.LoadGameObjectForQuests();

            Log.outInfo(LogFilter.ServerLoading, "Loading BattleMasters...");
            Global.BattlegroundMgr.LoadBattleMastersEntry();

            Log.outInfo(LogFilter.ServerLoading, "Loading GameTeleports...");
            Global.ObjectMgr.LoadGameTele();

            Log.outInfo(LogFilter.ServerLoading, "Loading Trainers...");
            Global.ObjectMgr.LoadTrainers();                                // must be after load CreatureTemplate

            Log.outInfo(LogFilter.ServerLoading, "Loading Gossip menu...");
            Global.ObjectMgr.LoadGossipMenu();

            Log.outInfo(LogFilter.ServerLoading, "Loading Gossip menu options...");
            Global.ObjectMgr.LoadGossipMenuItems();

            Log.outInfo(LogFilter.ServerLoading, "Loading Gossip menu addon...");
            Global.ObjectMgr.LoadGossipMenuAddon();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Template Gossip...");
            Global.ObjectMgr.LoadCreatureTemplateGossip();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature trainers...");
            Global.ObjectMgr.LoadCreatureTrainers();                         // must be after LoadGossipMenuItems

            Log.outInfo(LogFilter.ServerLoading, "Loading Vendors...");
            Global.ObjectMgr.LoadVendors();                                  // must be after load CreatureTemplate and ItemTemplate

            Log.outInfo(LogFilter.ServerLoading, "Loading Waypoints...");
            Global.WaypointMgr.LoadPaths();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Formations...");
            FormationMgr.LoadCreatureFormations();

            Log.outInfo(LogFilter.ServerLoading, "Loading World State templates...");
            Global.WorldStateMgr.LoadFromDB();                                          // must be loaded before battleground, outdoor PvP and conditions

            Log.outInfo(LogFilter.ServerLoading, "Loading Persistend World Variables...");              // must be loaded before Battleground, outdoor PvP and conditions
            LoadPersistentWorldVariables();

            Global.WorldStateMgr.SetValue(WorldStates.CurrentPvpSeasonId, WorldConfig.Values[WorldCfg.ArenaSeasonInProgress].Bool ? WorldConfig.Values[WorldCfg.ArenaSeasonId].Int32 : 0, false, null);
            Global.WorldStateMgr.SetValue(WorldStates.PreviousPvpSeasonId, WorldConfig.Values[WorldCfg.ArenaSeasonId].Int32 - (WorldConfig.Values[WorldCfg.ArenaSeasonInProgress].Bool ? 1 : 0), false, null);

            Global.ObjectMgr.LoadPhases();

            Log.outInfo(LogFilter.ServerLoading, "Loading Conditions...");
            Global.ConditionMgr.LoadConditions();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change achievement pairs...");
            Global.ObjectMgr.LoadFactionChangeAchievements();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change spell pairs...");
            Global.ObjectMgr.LoadFactionChangeSpells();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change item pairs...");
            Global.ObjectMgr.LoadFactionChangeItems();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change quest pairs...");
            Global.ObjectMgr.LoadFactionChangeQuests();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change reputation pairs...");
            Global.ObjectMgr.LoadFactionChangeReputations();

            Log.outInfo(LogFilter.ServerLoading, "Loading faction change title pairs...");
            Global.ObjectMgr.LoadFactionChangeTitles();

            Log.outInfo(LogFilter.ServerLoading, "Loading mount definitions...");
            CollectionMgr.LoadMountDefinitions();

            Log.outInfo(LogFilter.ServerLoading, "Loading GM bugs...");
            Global.SupportMgr.LoadBugTickets();

            Log.outInfo(LogFilter.ServerLoading, "Loading GM complaints...");
            Global.SupportMgr.LoadComplaintTickets();

            Log.outInfo(LogFilter.ServerLoading, "Loading GM suggestions...");
            Global.SupportMgr.LoadSuggestionTickets();

            //Log.outInfo(LogFilter.ServerLoading, "Loading GM surveys...");
            //Global.SupportMgr.LoadSurveys();

            // Handle outdated emails (delete/return)
            Log.outInfo(LogFilter.ServerLoading, "Returning old mails...");
            Global.ObjectMgr.ReturnOrDeleteOldMails();

            Log.outInfo(LogFilter.ServerLoading, "Loading Autobroadcasts...");
            LoadAutobroadcasts();

            // Load and initialize scripts
            Global.ObjectMgr.LoadSpellScripts();                              // must be after load Creature/Gameobject(Template/Data)
            Global.ObjectMgr.LoadEventScripts();                              // must be after load Creature/Gameobject(Template/Data)

            Log.outInfo(LogFilter.ServerLoading, "Loading spell script names...");
            Global.ObjectMgr.LoadSpellScriptNames();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Texts...");
            Global.CreatureTextMgr.LoadCreatureTexts();

            Log.outInfo(LogFilter.ServerLoading, "Loading Creature Text Locales...");
            Global.CreatureTextMgr.LoadCreatureTextLocales();

            Log.outInfo(LogFilter.ServerLoading, "Initializing Scripts...");
            Global.ScriptMgr.Initialize();
            Global.ScriptMgr.OnConfigLoad(false);                                // must be done after the ScriptMgr has been properly initialized

            Log.outInfo(LogFilter.ServerLoading, "Validating spell scripts...");
            Global.ObjectMgr.ValidateSpellScripts();

            Log.outInfo(LogFilter.ServerLoading, "Loading SmartAI scripts...");
            Global.SmartAIMgr.LoadFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Loading Calendar data...");
            Global.CalendarMgr.LoadFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Loading Petitions...");
            Global.PetitionMgr.LoadPetitions();

            Log.outInfo(LogFilter.ServerLoading, "Loading Signatures...");
            Global.PetitionMgr.LoadSignatures();

            Log.outInfo(LogFilter.ServerLoading, "Loading Item loot...");
            Global.LootItemStorage.LoadStorageFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Initialize query data...");
            Global.ObjectMgr.InitializeQueriesData(QueryDataGroup.All);

            // Initialize Loop-time and timers
            Log.outInfo(LogFilter.ServerLoading, "Initialize Loop-time and timers");
            LoopTime.Update(Time.Now.ToServerTime());

            DB.Login.Execute(
                $"INSERT INTO uptime (realmid, starttime, uptime, revision) " +
                $"VALUES({_realm.Id.Index}, {(UnixTime64)Time.ApplicationStartTime}, 0, '{string.Empty}')");       // One-time query

            m_timers[WorldTimers.Auctions].SetInterval((Minutes)1);
            m_timers[WorldTimers.AuctionsPending].SetInterval((Milliseconds)250);

            //Update "uptime" table based on configuration entry in minutes.
            m_timers[WorldTimers.UpTime].SetInterval((Minutes)10);
            //erase corpses every 20 minutes
            m_timers[WorldTimers.Corpses].SetInterval((Minutes)20);
            m_timers[WorldTimers.CleanDB].SetInterval(WorldConfig.Values[WorldCfg.LogdbClearinterval].TimeSpan);
            m_timers[WorldTimers.AutoBroadcast].SetInterval(WorldConfig.Values[WorldCfg.AutoBroadcastInterval].TimeSpan);
            // check for chars to delete every day
            m_timers[WorldTimers.DeleteChars].SetInterval((Days)1);
            // for AhBot
            m_timers[WorldTimers.AhBot].SetInterval(WorldConfig.Values[WorldCfg.AhbotUpdateInterval].TimeSpan);
            m_timers[WorldTimers.GuildSave].SetInterval(WorldConfig.Values[WorldCfg.GuildSaveInterval].TimeSpan);

            m_timers[WorldTimers.Blackmarket].SetInterval((Seconds)10);

            blackmarketNextRefreshTime = ServerTime.Zero;

            m_timers[WorldTimers.WhoList].SetInterval((Seconds)5); // update who list cache every 5 seconds

            m_timers[WorldTimers.ChannelSave].SetInterval(WorldConfig.Values[WorldCfg.PreserveCustomChannelInterval].TimeSpan);

            //mailExpires is checked when updating auctions
            mailExpiresTime = RealmTime.Zero;            

            //- Initialize MapManager
            Log.outInfo(LogFilter.ServerLoading, "Starting Map System");
            Global.MapMgr.Initialize();

            Log.outInfo(LogFilter.ServerLoading, "Starting Game Event system...");
            TimeSpan nextGameEvent = Global.GameEventMgr.StartSystem();
            m_timers[WorldTimers.Events].SetInterval(nextGameEvent);    //depend on next event

            // Delete all characters which have been deleted X days before
            Player.DeleteOldCharacters();

            Log.outInfo(LogFilter.ServerLoading, "Initializing chat channels...");
            ChannelManager.LoadFromDB();

            Log.outInfo(LogFilter.ServerLoading, "Initializing Opcodes...");
            PacketManager.Initialize();

            Log.outInfo(LogFilter.ServerLoading, "Starting Arena Season...");
            Global.GameEventMgr.StartArenaSeason();

            Global.SupportMgr.Initialize();

            // Initialize Battlegrounds
            Log.outInfo(LogFilter.ServerLoading, "Starting BattlegroundSystem");
            Global.BattlegroundMgr.LoadBattlegroundTemplates();

            // Initialize outdoor pvp
            Log.outInfo(LogFilter.ServerLoading, "Starting Outdoor PvP System");
            Global.OutdoorPvPMgr.InitOutdoorPvP();

            // Initialize Battlefield
            Log.outInfo(LogFilter.ServerLoading, "Starting Battlefield System");
            Global.BattleFieldMgr.InitBattlefield();

            // Initialize Warden
            Log.outInfo(LogFilter.ServerLoading, "Loading Warden Checks...");
            Global.WardenCheckMgr.LoadWardenChecks();

            Log.outInfo(LogFilter.ServerLoading, "Loading Warden Action Overrides...");
            Global.WardenCheckMgr.LoadWardenOverrides();

            Log.outInfo(LogFilter.ServerLoading, "Deleting expired bans...");
            DB.Login.Execute("DELETE FROM ip_banned WHERE unbandate <= UNIX_TIMESTAMP() AND unbandate<>bandate");      // One-time query

            Log.outInfo(LogFilter.ServerLoading, "Initializing quest reset times...");
            InitQuestResetTimes();
            CheckScheduledResetTimes();

            Log.outInfo(LogFilter.ServerLoading, "Calculate random battleground reset time...");
            InitRandomBGResetTime();

            Log.outInfo(LogFilter.ServerLoading, "Calculate deletion of old calendar events time...");
            InitCalendarOldEventsDeletionTime();

            Log.outInfo(LogFilter.ServerLoading, "Calculate Guild cap reset time...");
            InitGuildResetTime();

            Log.outInfo(LogFilter.ServerLoading, "Calculate next currency reset time...");
            InitCurrencyResetTime();

            Log.outInfo(LogFilter.ServerLoading, "Loading race and class expansion requirements...");
            Global.ObjectMgr.LoadRaceAndClassExpansionRequirements();

            Log.outInfo(LogFilter.ServerLoading, "Loading character templates...");
            Global.CharacterTemplateDataStorage.LoadCharacterTemplates();

            Log.outInfo(LogFilter.ServerLoading, "Loading battle pets info...");
            BattlePetMgr.Initialize();

            Log.outInfo(LogFilter.ServerLoading, "Loading scenarios");
            Global.ScenarioMgr.LoadDB2Data();
            Global.ScenarioMgr.LoadDBData();

            Log.outInfo(LogFilter.ServerLoading, "Loading scenario poi data");
            Global.ScenarioMgr.LoadScenarioPOI();

            Log.outInfo(LogFilter.ServerLoading, "Loading phase names...");
            Global.ObjectMgr.LoadPhaseNames();

            Log.outInfo(LogFilter.ServerLoading, "Loading map corpse positions...");
            Global.ObjectMgr.LoadMapCorpsePositions();
        }

        public void LoadConfigSettings(bool reload = false)
        {
            WorldConfig.Load(reload);

            if (!Timezone.Initialized)
            {
                TimeSpan configTimeZoneOffsetValue = WorldConfig.Values[WorldCfg.TimeZoneOffset].TimeSpan;

                if (configTimeZoneOffsetValue < Timezone.MinimalTimeZoneOffset)
                    Timezone.Initialize(TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now));
                else if (configTimeZoneOffsetValue > Timezone.MaximalTimeZoneOffset)
                    Timezone.Initialize((RealmZone)WorldConfig.Values[WorldCfg.RealmZone].Int32);
                else
                    Timezone.Initialize(configTimeZoneOffsetValue);
            }

            m_defaultDbcLocale = ConfigMgr.GetDefaultEnumValue("DBC.Locale", Locale.enUS);

            if (m_defaultDbcLocale >= Locale.Total || m_defaultDbcLocale == Locale.None)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Incorrect DBC.Locale! Must be >= 0 and < {(int)Locale.Total} and not {(int)Locale.None} (set to 0)");

                m_defaultDbcLocale = Locale.enUS;
            }

            Log.outInfo(LogFilter.ServerLoading, $"Using {m_defaultDbcLocale} DBC Locale");

            // load update time related configs
            _worldUpdateTime.LoadFromConfig();

            SetPlayerAmountLimit(ConfigMgr.GetDefaultValue("PlayerLimit", 100));
            SetMotd(ConfigMgr.GetDefaultValue("Motd", "Welcome to a Cypher Core Server."));

            if (reload)
            {
                Global.SupportMgr.SetSupportSystemStatus(WorldConfig.Values[WorldCfg.SupportEnabled].Bool);
                Global.SupportMgr.SetTicketSystemStatus(WorldConfig.Values[WorldCfg.SupportTicketsEnabled].Bool);
                Global.SupportMgr.SetBugSystemStatus(WorldConfig.Values[WorldCfg.SupportBugsEnabled].Bool);
                Global.SupportMgr.SetComplaintSystemStatus(WorldConfig.Values[WorldCfg.SupportComplaintsEnabled].Bool);
                Global.SupportMgr.SetSuggestionSystemStatus(WorldConfig.Values[WorldCfg.SupportSuggestionsEnabled].Bool);              

                Global.MapMgr.SetMapUpdateInterval(WorldConfig.Values[WorldCfg.IntervalMapUpdate].TimeSpan);
                Global.MapMgr.SetGridCleanUpDelay(WorldConfig.Values[WorldCfg.IntervalGridClean].TimeSpan);

                m_timers[WorldTimers.UpTime].SetInterval(WorldConfig.Values[WorldCfg.UptimeUpdate].TimeSpan);
                m_timers[WorldTimers.UpTime].Reset();

                m_timers[WorldTimers.CleanDB].SetInterval(WorldConfig.Values[WorldCfg.LogdbClearinterval].TimeSpan);
                m_timers[WorldTimers.CleanDB].Reset();


                m_timers[WorldTimers.AutoBroadcast].SetInterval(WorldConfig.Values[WorldCfg.AutoBroadcastInterval].TimeSpan);
                m_timers[WorldTimers.AutoBroadcast].Reset();
            }

            for (byte i = 0; i < (int)UnitMoveType.Max; ++i)
                SharedConst.playerBaseMoveSpeed[i] = SharedConst.baseMoveSpeed[i] * WorldConfig.Values[WorldCfg.RateMovespeed].Float;

            float rateCreatureAggro = WorldConfig.Values[WorldCfg.RateCreatureAggro].Float;

            //visibility on continents
            m_MaxVisibleDistanceOnContinents = ConfigMgr.GetDefaultValue("Visibility.Distance.Continents", SharedConst.DefaultVisibilityDistance);
            if (m_MaxVisibleDistanceOnContinents < 45 * rateCreatureAggro)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Continents can't be less max aggro radius {45 * rateCreatureAggro}");

                m_MaxVisibleDistanceOnContinents = 45 * rateCreatureAggro;
            }
            else if (m_MaxVisibleDistanceOnContinents > SharedConst.MaxVisibilityDistance)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Continents can't be greater {SharedConst.MaxVisibilityDistance}");

                m_MaxVisibleDistanceOnContinents = SharedConst.MaxVisibilityDistance;
            }

            //visibility in instances
            m_MaxVisibleDistanceInInstances = ConfigMgr.GetDefaultValue("Visibility.Distance.Instances", SharedConst.DefaultVisibilityInstance);
            if (m_MaxVisibleDistanceInInstances < 45 * rateCreatureAggro)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Instances can't be less max aggro radius {45 * rateCreatureAggro}");

                m_MaxVisibleDistanceInInstances = 45 * rateCreatureAggro;
            }
            else if (m_MaxVisibleDistanceInInstances > SharedConst.MaxVisibilityDistance)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Instances can't be greater {SharedConst.MaxVisibilityDistance}");

                m_MaxVisibleDistanceInInstances = SharedConst.MaxVisibilityDistance;
            }

            //visibility in BG
            m_MaxVisibleDistanceInBG = ConfigMgr.GetDefaultValue("Visibility.Distance.BG", SharedConst.DefaultVisibilityBGAreans);
            if (m_MaxVisibleDistanceInBG < 45 * rateCreatureAggro)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.BG can't be less max aggro radius {45 * rateCreatureAggro}");

                m_MaxVisibleDistanceInBG = 45 * rateCreatureAggro;
            }
            else if (m_MaxVisibleDistanceInBG > SharedConst.MaxVisibilityDistance)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.BG can't be greater {SharedConst.MaxVisibilityDistance}");

                m_MaxVisibleDistanceInBG = SharedConst.MaxVisibilityDistance;
            }

            // Visibility in Arenas
            m_MaxVisibleDistanceInArenas = ConfigMgr.GetDefaultValue("Visibility.Distance.Arenas", SharedConst.DefaultVisibilityBGAreans);
            if (m_MaxVisibleDistanceInArenas < 45 * rateCreatureAggro)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Arenas can't be less max aggro radius {45 * rateCreatureAggro}");

                m_MaxVisibleDistanceInArenas = 45 * rateCreatureAggro;
            }
            else if (m_MaxVisibleDistanceInArenas > SharedConst.MaxVisibilityDistance)
            {
                Log.outError(LogFilter.ServerLoading, 
                    $"Visibility.Distance.Arenas can't be greater {SharedConst.MaxVisibilityDistance}");

                m_MaxVisibleDistanceInArenas = SharedConst.MaxVisibilityDistance;
            }

            m_visibility_notify_periodOnContinents = ConfigMgr.GetDefaultValue("Visibility.Notify.Period.OnContinents", SharedConst.DefaultVisibilityNotifyPeriod);
            m_visibility_notify_periodInInstances = ConfigMgr.GetDefaultValue("Visibility.Notify.Period.InInstances", SharedConst.DefaultVisibilityNotifyPeriod);
            m_visibility_notify_periodInBG = ConfigMgr.GetDefaultValue("Visibility.Notify.Period.InBG", SharedConst.DefaultVisibilityNotifyPeriod);
            m_visibility_notify_periodInArenas = ConfigMgr.GetDefaultValue("Visibility.Notify.Period.InArenas", SharedConst.DefaultVisibilityNotifyPeriod);

            _guidWarningMsg = WorldConfig.GetDefaultValue(
                "Respawn.WarningMessage", "There will be an unscheduled server restart at 03:00. " +
                "The server will be available again shortly after.");

            _alertRestartReason = WorldConfig.GetDefaultValue("Respawn.AlertRestartReason", "Urgent Maintenance");

            string dataPath = ConfigMgr.GetDefaultValue("DataDir", "./");
            if (reload)
            {
                if (dataPath != _dataPath)
                {
                    Log.outError(LogFilter.ServerLoading,
                        $"DataDir option can't be changed at worldserver.conf reload, " +
                        $"using current value ({_dataPath}).");
                }
            }
            else
            {
                _dataPath = dataPath;
                Log.outInfo(LogFilter.ServerLoading, $"Using DataDir {_dataPath}");
            }

            Log.outInfo(LogFilter.ServerLoading, $@"WORLD: MMap data directory is: {_dataPath}\mmaps");

            bool EnableIndoor = ConfigMgr.GetDefaultValue("vmap.EnableIndoorCheck", true);
            bool EnableLOS = ConfigMgr.GetDefaultValue("vmap.EnableLOS", true);
            bool EnableHeight = ConfigMgr.GetDefaultValue("vmap.EnableHeight", true);

            if (!EnableHeight)
            {
                Log.outError(LogFilter.ServerLoading,
                    "VMap height checking Disabled! Creatures movements " +
                    "and other various things WILL be broken! Expect no support.");
            }

            Global.VMapMgr.SetEnableLineOfSightCalc(EnableLOS);
            Global.VMapMgr.SetEnableHeightCalc(EnableHeight);

            Log.outInfo(LogFilter.ServerLoading, 
                $"VMap support included. LineOfSight: {EnableLOS}, getHeight: {EnableHeight}, indoorCheck: {EnableIndoor}");

            Log.outInfo(LogFilter.ServerLoading, $@"VMap data directory is: {GetDataPath()}\vmaps");
        }

        public void SetForcedWarModeFactionBalanceState(int team, int reward = 0)
        {
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.WarModeHordeBuffValue, 10 + (team == BattleGroundTeamId.Alliance ? reward : 0), false, null);
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.WarModeAllianceBuffValue, 10 + (team == BattleGroundTeamId.Horde ? reward : 0), false, null);
        }

        public void DisableForcedWarModeFactionBalanceState()
        {
            UpdateWarModeRewardValues();
        }

        public void LoadAutobroadcasts()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            m_Autobroadcasts.Clear();

            PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_AUTOBROADCAST);
            stmt.SetInt32(0, _realm.Id.Index);

            SQLResult result = DB.Login.Query(stmt);
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded 0 autobroadcasts definitions. " +
                    "DB table `autobroadcast` is empty for this realm!");
                return;
            }

            do
            {
                byte id = result.Read<byte>(0);

                m_Autobroadcasts[id] = new Autobroadcast(result.Read<string>(2), result.Read<byte>(1));

            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {m_Autobroadcasts.Count} autobroadcast definitions in {Time.Diff(oldMSTime)} ms.");
        }

        public void UpdateShutDownMessage(ServerTime newServerTime)
        {
            ///- check for shutdown time
            if (!IsStopped && IsShuttingDown)
            {
                //- ... and it is overdue, stop the world
                if (m_ShutdownTime <= newServerTime)
                {
                    if (!m_ShutdownMask.HasAnyFlag(ShutdownMask.Idle) || GetActiveAndQueuedSessionCount() == 0)
                        IsStopped = true;                         // exist code already set
                    else
                        m_ShutdownTime = newServerTime + (Seconds)1;       // minimum timer value to wait idle state
                }
                //- ... else decrease it and if necessary display a shutdown countdown to the users
                else
                {
                    ShutdownMsg();
                }
            }
        }

        public void Update(TimeSpan diff)
        {
            _worldUpdateTime.Update((Milliseconds)diff);

            // Record update if recording set in log and diff is greater then minimum set in log
            _worldUpdateTime.RecordUpdateTime((Milliseconds)diff, GetActiveSessionCount());

            // Update the different timers
            for (WorldTimers i = 0; i < WorldTimers.Max; ++i)
            {
                if (m_timers[i].GetCurrent() >= TimeSpan.Zero)
                    m_timers[i].Update(diff);
                else
                    m_timers[i].SetCurrent(TimeSpan.Zero);
            }

            // Update Who List Storage
            if (m_timers[WorldTimers.WhoList].Passed())
            {
                m_timers[WorldTimers.WhoList].Reset();
                Global.WhoListStorageMgr.Update();
            }

            if (IsStopped || m_timers[WorldTimers.ChannelSave].Passed())
            {
                m_timers[WorldTimers.ChannelSave].Reset();

                if (WorldConfig.Values[WorldCfg.PreserveCustomChannels].Bool)
                {
                    ChannelManager mgr1 = ChannelManager.ForTeam(Team.Alliance);
                    mgr1.SaveToDB();
                    ChannelManager mgr2 = ChannelManager.ForTeam(Team.Horde);
                    if (mgr1 != mgr2)
                        mgr2.SaveToDB();
                }
            }

            CheckScheduledResetTimes();

            if (LoopTime.RealmTime > m_NextRandomBGReset)
                ResetRandomBG();

            if (LoopTime.RealmTime > m_NextCalendarOldEventsDeletionTime)
                CalendarDeleteOldEvents();

            if (LoopTime.RealmTime > m_NextGuildReset)
                ResetGuildCap();

            if (LoopTime.RealmTime > m_NextCurrencyReset)
                ResetCurrencyWeekCap();

            //Handle auctions when the timer has passed
            if (m_timers[WorldTimers.Auctions].Passed())
            {
                m_timers[WorldTimers.Auctions].Reset();

                bool firstUpdate = mailExpiresTime == RealmTime.Zero;     // timer is 0 on startup
                                
                if (mailExpiresTime <= LoopTime.RealmTime)
                {
                    if (!firstUpdate)
                    {
                        Global.ObjectMgr.ReturnOrDeleteOldMails(LoopTime.ServerTime, LoopTime.RealmTime);
                    }

                    // Update mails (return old mails with item, or delete them)
                    // to set mailtimer to return mails every day between 4 and 5 am
                    mailExpiresTime = LoopTime.RealmTime.Date + WorldConfig.Values[WorldCfg.CleanOldMailTime].TimeSpan;

                    if (mailExpiresTime <= LoopTime.RealmTime)
                        mailExpiresTime += (Days)1;
                }

                // Handle expired auctions
                Global.AuctionHouseMgr.Update();
            }

            if (m_timers[WorldTimers.AuctionsPending].Passed())
            {
                m_timers[WorldTimers.AuctionsPending].Reset();

                Global.AuctionHouseMgr.UpdatePendingAuctions();
            }

            if (m_timers[WorldTimers.Blackmarket].Passed())
            {
                m_timers[WorldTimers.Blackmarket].Reset();

                bool firstUpdate = blackmarketNextRefreshTime == Time.Zero;     // timer is 0 on startup

                //- Update blackmarket, refresh auctions if necessary                
                if (blackmarketNextRefreshTime <= LoopTime.ServerTime || firstUpdate)                
                {
                    Global.BlackMarketMgr.RefreshAuctions();

                    ServerTime previousPoint = firstUpdate ? LoopTime.ServerTime : blackmarketNextRefreshTime;
                    TimeSpan refreshPeriod = WorldConfig.Values[WorldCfg.BlackmarketUpdatePeriod].TimeSpan;

                    blackmarketNextRefreshTime = previousPoint + refreshPeriod;
                }
                else
                {
                    Global.BlackMarketMgr.Update();
                }
            }

            //Handle session updates when the timer has passed
            _worldUpdateTime.RecordUpdateTimeReset();
            UpdateSessions(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("UpdateSessions");

            // <li> Update uptime table
            if (m_timers[WorldTimers.UpTime].Passed())
            {
                Seconds tmpDiff = (Seconds)LoopTime.UpTime;
                int maxOnlinePlayers = GetMaxPlayerCount();

                m_timers[WorldTimers.UpTime].Reset();

                PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_UPTIME_PLAYERS);

                stmt.SetInt32(0, tmpDiff);
                stmt.SetInt32(1, maxOnlinePlayers);
                stmt.SetInt32(2, _realm.Id.Index);
                stmt.SetInt32(3, (UnixTime)Time.ApplicationStartTime);

                DB.Login.Execute(stmt);
            }

            // <li> Clean logs table
            if (WorldConfig.Values[WorldCfg.LogdbCleartime].Seconds > 0) // if not enabled, ignore the timer
            {
                if (m_timers[WorldTimers.CleanDB].Passed())
                {
                    m_timers[WorldTimers.CleanDB].Reset();

                    PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_OLD_LOGS);
                    stmt.SetInt32(0, WorldConfig.Values[WorldCfg.LogdbCleartime].Seconds);
                    stmt.SetInt32(1, 0);
                    stmt.SetInt32(2, GetRealm().Id.Index);

                    DB.Login.Execute(stmt);
                }
            }

            _worldUpdateTime.RecordUpdateTimeReset();
            Global.MapMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("UpdateMapMgr");

            Global.TerrainMgr.Update(diff);

            if (WorldConfig.Values[WorldCfg.AutoBroadcast].Bool)
            {
                if (m_timers[WorldTimers.AutoBroadcast].Passed())
                {
                    m_timers[WorldTimers.AutoBroadcast].Reset();
                    SendAutoBroadcast();
                }
            }

            Global.BattlegroundMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("UpdateBattlegroundMgr");

            Global.OutdoorPvPMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("UpdateOutdoorPvPMgr");

            Global.BattleFieldMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("BattlefieldMgr");

            //- Delete all characters which have been deleted X days before
            if (m_timers[WorldTimers.DeleteChars].Passed())
            {
                m_timers[WorldTimers.DeleteChars].Reset();
                Player.DeleteOldCharacters();
            }

            Global.LFGMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("UpdateLFGMgr");

            Global.GroupMgr.Update(diff);
            _worldUpdateTime.RecordUpdateTimeDuration("GroupMgr");

            // execute callbacks from sql queries that were queued recently
            ProcessQueryCallbacks();
            _worldUpdateTime.RecordUpdateTimeDuration("ProcessQueryCallbacks");

            // Erase corpses once every 20 minutes
            if (m_timers[WorldTimers.Corpses].Passed())
            {
                m_timers[WorldTimers.Corpses].Reset();
                Global.MapMgr.DoForAllMaps(map => map.RemoveOldCorpses());
            }

            // Process Game events when necessary
            if (m_timers[WorldTimers.Events].Passed())
            {
                m_timers[WorldTimers.Events].Reset();                   // to give time for Update() to be processed
                TimeSpan nextGameEvent = Global.GameEventMgr.Update();
                m_timers[WorldTimers.Events].SetInterval(nextGameEvent);
                m_timers[WorldTimers.Events].Reset();
            }

            if (m_timers[WorldTimers.GuildSave].Passed())
            {
                m_timers[WorldTimers.GuildSave].Reset();
                Global.GuildMgr.SaveGuilds();
            }

            // Check for shutdown warning
            if (_guidWarn && !_guidAlert)
            {
                _warnDiff += diff;
                if (LoopTime.ServerTime >= _warnShutdownTime)
                    DoGuidWarningRestart();
                else if (_warnDiff > WorldConfig.Values[WorldCfg.RespawnGuidWarningFrequency].TimeSpan)
                    SendGuidWarning();
            }

            Global.ScriptMgr.OnWorldUpdate(diff);
        }

        public void ForceGameEventUpdate()
        {
            m_timers[WorldTimers.Events].Reset();                   // to give time for Update() to be processed
            TimeSpan nextGameEvent = Global.GameEventMgr.Update();
            m_timers[WorldTimers.Events].SetInterval(nextGameEvent);
            m_timers[WorldTimers.Events].Reset();
        }

        public void SendGlobalMessage(ServerPacket packet, WorldSession self = null, Team team = 0)
        {
            foreach (var session in m_sessions.Values)
            {
                if (session.GetPlayer() != null && session.GetPlayer().IsInWorld && session != self &&
                    (team == 0 || session.GetPlayer().GetTeam() == team))
                {
                    session.SendPacket(packet);
                }
            }
        }

        public void SendGlobalGMMessage(ServerPacket packet, WorldSession self = null, Team team = 0)
        {
            foreach (var session in m_sessions.Values)
            {
                // check if session and can receive global GM Messages and its not self
                if (session == null || session == self || !session.HasPermission(RBACPermissions.ReceiveGlobalGmTextmessage))
                    continue;

                // Player should be in world
                Player player = session.GetPlayer();
                if (player == null || !player.IsInWorld)
                    continue;

                // Send only to same team, if team is given
                if (team == 0 || player.GetTeam() == team)
                    session.SendPacket(packet);
            }
        }

        // Send a System Message to all players (except self if mentioned)
        public void SendWorldText(CypherStrings string_id, params object[] args)
        {
            WorldWorldTextBuilder wt_builder = new((uint)string_id, args);
            var wt_do = new LocalizedDo(wt_builder);
            foreach (var session in m_sessions.Values)
            {
                if (session == null || session.GetPlayer() == null || !session.GetPlayer().IsInWorld)
                    continue;

                wt_do.Invoke(session.GetPlayer());
            }
        }

        // Send a System Message to all GMs (except self if mentioned)
        public void SendGMText(CypherStrings string_id, params object[] args)
        {
            var wt_builder = new WorldWorldTextBuilder((uint)string_id, args);
            var wt_do = new LocalizedDo(wt_builder);
            foreach (var session in m_sessions.Values)
            {
                // Session should have permissions to receive global gm messages
                if (session == null || !session.HasPermission(RBACPermissions.ReceiveGlobalGmTextmessage))
                    continue;

                // Player should be in world
                Player player = session.GetPlayer();
                if (player == null || !player.IsInWorld)
                    continue;

                wt_do.Invoke(player);
            }
        }

        // Send a packet to all players (or players selected team) in the zone (except self if mentioned)
        public bool SendZoneMessage(int zone, ServerPacket packet, WorldSession self = null, Team team = 0)
        {
            bool foundPlayerToSend = false;
            foreach (var session in m_sessions.Values)
            {
                if (session != null && session.GetPlayer() != null && session.GetPlayer().IsInWorld &&
                    session.GetPlayer().GetZoneId() == zone && session != self && (team == 0 || session.GetPlayer().GetTeam() == team))
                {
                    session.SendPacket(packet);
                    foundPlayerToSend = true;
                }
            }

            return foundPlayerToSend;
        }

        // Send a System Message to all players in the zone (except self if mentioned)
        public void SendZoneText(int zone, string text, WorldSession self = null, Team team = 0)
        {
            ChatPkt data = new();
            data.Initialize(ChatMsg.System, Language.Universal, null, null, text);
            SendZoneMessage(zone, data, self, team);
        }

        public void KickAll()
        {
            m_QueuedPlayer.Clear();                                 // prevent send queue update packet and login queued sessions

            // session not removed at kick and will removed in next update tick
            foreach (var session in m_sessions.Values)
                session.KickPlayer("World::KickAll");
        }

        void KickAllLess(AccountTypes sec)
        {
            // session not removed at kick and will removed in next update tick
            foreach (var session in m_sessions.Values)
                if (session.GetSecurity() < sec)
                    session.KickPlayer("World::KickAllLess");
        }

        /// Ban an account or ban an IP address, duration will be parsed using TimeStringToSecs if it is positive, otherwise permban
        public BanReturn BanAccount(BanMode mode, string nameOrIP, string duration, string reason, string author)
        {
            Seconds duration_secs = Time.StringToSecs(duration);
            return BanAccount(mode, nameOrIP, duration_secs, reason, author);
        }

        /// Ban an account or ban an IP address, duration is in seconds if positive, otherwise permban
        public BanReturn BanAccount(BanMode mode, string nameOrIP, Seconds duration_secs, string reason, string author)
        {
            // Prevent banning an already banned account
            if (mode == BanMode.Account && Global.AccountMgr.IsBannedAccount(nameOrIP))
                return BanReturn.Exists;

            
            PreparedStatement stmt;
            IDataBase dataBase = null;

            // Update the database with ban information
            switch (mode)
            {
                case BanMode.IP:
                    // No SQL injection with prepared statements
                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_BY_IP);
                    stmt.SetString(0, nameOrIP);
                    dataBase = DB.Login;

                    var stmt2 = LoginDatabase.GetPreparedStatement(LoginStatements.INS_IP_BANNED);
                    stmt2.SetString(0, nameOrIP);
                    stmt2.SetInt32(1, duration_secs);
                    stmt2.SetString(2, author);
                    stmt2.SetString(3, reason);
                    DB.Login.Execute(stmt2);
                    break;
                case BanMode.Account:
                    // No SQL injection with prepared statements
                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_ID_BY_NAME);
                    stmt.SetString(0, nameOrIP);
                    dataBase = DB.Login;
                    break;
                case BanMode.Character:
                    // No SQL injection with prepared statements
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_ACCOUNT_BY_NAME);
                    stmt.SetString(0, nameOrIP);
                    dataBase = DB.Characters;
                    break;
                default:
                    return BanReturn.SyntaxError;
            }

            SQLResult resultAccounts = dataBase.Query(stmt);

            if (resultAccounts.IsEmpty())
            {
                if (mode == BanMode.IP)
                    return BanReturn.Success;                             // ip correctly banned but nobody affected (yet)
                else
                    return BanReturn.Notfound;                            // Nobody to ban
            }

            // Disconnect all affected players (for IP it can be several)
            SQLTransaction trans = new();
            do
            {
                int account = resultAccounts.Read<int>(0);

                if (mode != BanMode.IP)
                {
                    // make sure there is only one active ban
                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_ACCOUNT_NOT_BANNED);
                    stmt.SetInt32(0, account);
                    trans.Append(stmt);
                    // No SQL injection with prepared statements
                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_ACCOUNT_BANNED);
                    stmt.SetInt32(0, account);
                    stmt.SetInt32(1, duration_secs);
                    stmt.SetString(2, author);
                    stmt.SetString(3, reason);
                    trans.Append(stmt);
                }

                WorldSession sess = FindSession(account);
                if (sess != null)
                {
                    if (sess.GetPlayerName() != author)
                        sess.KickPlayer("World::BanAccount Banning account");
                }
            } while (resultAccounts.NextRow());

            DB.Login.CommitTransaction(trans);

            return BanReturn.Success;
        }

        /// Remove a ban from an account or IP address
        public bool RemoveBanAccount(BanMode mode, string nameOrIP)
        {
            PreparedStatement stmt;
            if (mode == BanMode.IP)
            {
                stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_IP_NOT_BANNED);
                stmt.SetString(0, nameOrIP);
                DB.Login.Execute(stmt);
            }
            else
            {
                int account = 0;
                if (mode == BanMode.Account)
                    account = Global.AccountMgr.GetId(nameOrIP);
                else if (mode == BanMode.Character)
                    account = Global.CharacterCacheStorage.GetCharacterAccountIdByName(nameOrIP);

                if (account == 0)
                    return false;

                //NO SQL injection as account is uint32
                stmt = LoginDatabase.GetPreparedStatement(LoginStatements.UPD_ACCOUNT_NOT_BANNED);
                stmt.SetInt32(0, account);
                DB.Login.Execute(stmt);
            }
            return true;
        }

        /// Ban an account or ban an IP address, duration will be parsed using TimeStringToSecs if it is positive, otherwise permban
        public BanReturn BanCharacter(string name, string duration, string reason, string author)
        {
            Seconds durationSecs = Time.StringToSecs(duration);
            return BanAccount(BanMode.Character, name, durationSecs, reason, author);
        }

        public BanReturn BanCharacter(string name, Seconds durationSecs, string reason, string author)
        {
            Player pBanned = Global.ObjAccessor.FindConnectedPlayerByName(name);
            ObjectGuid guid;

            // Pick a player to ban if not online
            if (pBanned == null)
            {
                guid = Global.CharacterCacheStorage.GetCharacterGuidByName(name);
                if (guid.IsEmpty())
                    return BanReturn.Notfound;                                    // Nobody to ban
            }
            else
                guid = pBanned.GetGUID();

            //Use transaction in order to ensure the order of the queries
            SQLTransaction trans = new();

            // make sure there is only one active ban
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER_BAN);
            stmt.SetInt64(0, guid.GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_BAN);
            stmt.SetInt64(0, guid.GetCounter());
            stmt.SetInt64(1, (long)durationSecs);
            stmt.SetString(2, author);
            stmt.SetString(3, reason);
            trans.Append(stmt);
            DB.Characters.CommitTransaction(trans);

            if (pBanned != null)
                pBanned.GetSession().KickPlayer("World::BanCharacter Banning character");

            return BanReturn.Success;
        }

        // Remove a ban from a character
        public bool RemoveBanCharacter(string name)
        {
            Player pBanned = Global.ObjAccessor.FindConnectedPlayerByName(name);
            ObjectGuid guid;

            // Pick a player to ban if not online
            if (pBanned == null)
            {
                guid = Global.CharacterCacheStorage.GetCharacterGuidByName(name);
                if (guid.IsEmpty())
                    return false;                                    // Nobody to ban
            }
            else
                guid = pBanned.GetGUID();

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER_BAN);
            stmt.SetInt64(0, guid.GetCounter());
            DB.Characters.Execute(stmt);
            return true;
        }

        public void ShutdownServ(Seconds time, ShutdownMask options, ShutdownExitCode exitcode, string reason = "")
        {
            // ignore if server shutdown at next tick
            if (IsStopped)
                return;

            m_ShutdownMask = options;
            m_ExitCode = exitcode;

            // If the shutdown time is 0, evaluate shutdown on next tick (no message)
            if (time == 0)
                m_ShutdownTime = LoopTime.ServerTime + (Seconds)1;
            // Else set the shutdown timer and warn users
            else
            {
                m_ShutdownTime = LoopTime.ServerTime + time;
                ShutdownMsg(true, null, reason);
            }

            Global.ScriptMgr.OnShutdownInitiate(exitcode, options);
        }

        public void ShutdownMsg(bool ignoreTimer = false, Player player = null, string reason = "")
        {
            // not show messages for idle shutdown mode
            if (m_ShutdownMask.HasAnyFlag(ShutdownMask.Idle))
                return;

            ServerTime currentTime = LoopTime.ServerTime;

            Seconds elapsed = (Seconds)(m_ShutdownTime - currentTime);
            Cypher.Assert(elapsed >= 0);

            if (ignoreTimer)
            {
                SendMessage();
            }
            else if (currentTime - m_lastShutdownMessageTime > (Seconds)1) // not show messages if timer is not changed yet
            {
                // Display a message every 12 hours, hours, 5 minutes, minute, 5 seconds and finally seconds
                if ((elapsed < (Minutes)5 && (elapsed % (Seconds)15) == 0) || // < 5 min; every 15 sec
                    (elapsed < (Minutes)15 && (elapsed % (Minutes)1) == 0) || // < 15 min ; every 1 min
                    (elapsed < (Minutes)30 && (elapsed % (Minutes)5) == 0) || // < 30 min ; every 5 min
                    (elapsed < (Hours)12 && (elapsed % (Hours)1) == 0) || // < 12 h ; every 1 h
                    (elapsed > (Hours)12 && (elapsed % (Hours)12) == 0)) // > 12 h ; every 12 h
                {
                    SendMessage();
                    m_lastShutdownMessageTime = currentTime;
                }
            }

            void SendMessage()
            {
                var str = Time.SpanToTimeString(elapsed, TimeFormat.Numeric);
                if (!reason.IsEmpty())
                    str += " - " + reason;

                ServerMessageType msgid = m_ShutdownMask.HasAnyFlag(ShutdownMask.Restart) ? ServerMessageType.RestartTime : ServerMessageType.ShutdownTime;

                SendServerMessage(msgid, str, player);

                Log.outDebug(LogFilter.Server, 
                    $"Server is {(m_ShutdownMask.HasAnyFlag(ShutdownMask.Restart) ? "restart" : "shuttingdown")} in {str}");
            }
        }

        public bool ShutdownCancel(out TimeSpan remainTime)
        {
            remainTime = TimeSpan.Zero;

            // nothing cancel or too late
            if (m_ShutdownTime == ServerTime.Zero || IsStopped)
                return false;

            ServerMessageType msgid = m_ShutdownMask.HasAnyFlag(ShutdownMask.Restart) ? ServerMessageType.RestartCancelled : ServerMessageType.ShutdownCancelled;

            ServerTime oldTime = m_ShutdownTime;
            m_ShutdownMask = 0;
            m_ShutdownTime = ServerTime.Zero;
            m_ExitCode = (byte)ShutdownExitCode.Shutdown;                       // to default value
            SendServerMessage(msgid);

            Log.outDebug(LogFilter.Server, 
                $"Server {(m_ShutdownMask.HasAnyFlag(ShutdownMask.Restart) ? "restart" : "shutdown")} cancelled.");

            Global.ScriptMgr.OnShutdownCancel();

            remainTime = m_ShutdownTime - LoopTime.ServerTime;
            return true;
        }

        public void SendServerMessage(ServerMessageType messageID, string stringParam = "", Player player = null)
        {
            ServerMessagesRecord serverMessage = CliDB.ServerMessagesStorage.LookupByKey((int)messageID);
            if (serverMessage == null)
                return;

            ChatServerMessage packet = new();
            packet.MessageID = (int)messageID;
            if (serverMessage.Text[player != null ? player.GetSession().GetSessionDbcLocale() : GetDefaultDbcLocale()].Contains("%s"))
                packet.StringParam = stringParam;

            if (player != null)
                player.SendPacket(packet);
            else
                SendGlobalMessage(packet);
        }

        public void UpdateSessions(TimeSpan diff)
        {
            Tuple<WorldSocket, long> linkInfo;
            while (_linkSocketQueue.TryDequeue(out linkInfo))
                ProcessLinkInstanceSocket(linkInfo);

            // Add new sessions
            WorldSession sess;
            while (addSessQueue.TryDequeue(out sess))
                AddSession_(sess);

            // Then send an update signal to remaining ones
            foreach (var pair in m_sessions)
            {
                WorldSession session = pair.Value;
                WorldSessionFilter updater = new(session);
                if (!session.Update(diff, updater))    // As interval = 0
                {
                    if (!RemoveQueuedPlayer(session) && session != null
                        && WorldConfig.Values[WorldCfg.IntervalDisconnectTolerance].Seconds != 0)
                    {
                        m_disconnects[session.GetAccountId()] = LoopTime.ServerTime;
                    }

                    RemoveQueuedPlayer(session);
                    m_sessions.TryRemove(pair.Key, out _);
                    m_sessionsByBnetGuid.Remove(session.GetBattlenetAccountGUID(), session);
                    session.Dispose();
                }
            }
        }

        void SendAutoBroadcast()
        {
            if (m_Autobroadcasts.Empty())
                return;

            var pair = m_Autobroadcasts.SelectRandomElementByWeight(autoPair => autoPair.Value.Weight);

            int abcenter = WorldConfig.Values[WorldCfg.AutoBroadcastCenter].Int32;

            if (abcenter == 0)
                SendWorldText(CypherStrings.AutoBroadcast, pair.Value.Message);
            else if (abcenter == 1)
                SendGlobalMessage(new PrintNotification(pair.Value.Message));
            else if (abcenter == 2)
            {
                SendWorldText(CypherStrings.AutoBroadcast, pair.Value.Message);
                SendGlobalMessage(new PrintNotification(pair.Value.Message));
            }

            Log.outDebug(LogFilter.Misc, $"AutoBroadcast: '{pair.Value.Message}'");
        }

        public void UpdateRealmCharCount(int accountId)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHARACTER_COUNT);
            stmt.SetInt32(0, accountId);
            _queryProcessor.AddCallback(DB.Characters.AsyncQuery(stmt).WithCallback(UpdateRealmCharCount));
        }

        void UpdateRealmCharCount(SQLResult result)
        {
            if (!result.IsEmpty())
            {
                uint Id = result.Read<uint>(0);
                uint charCount = result.Read<uint>(1);

                PreparedStatement stmt = LoginDatabase.GetPreparedStatement(LoginStatements.REP_REALM_CHARACTERS);
                stmt.SetUInt32(0, charCount);
                stmt.SetUInt32(1, Id);
                stmt.SetInt32(2, _realm.Id.Index);
                DB.Login.DirectExecute(stmt);
            }
        }

        void InitQuestResetTimes()
        {
            m_NextDailyQuestReset = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextDailyQuestResetTimeVarId);
            m_NextWeeklyQuestReset = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextWeeklyQuestResetTimeVarId);
            m_NextMonthlyQuestReset = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextMonthlyQuestResetTimeVarId);
        }

        static RealmTime GetNextDailyResetTime(RealmTime localTime)
        {
            return (RealmTime)GetHourTimestamp(localTime, WorldConfig.Values[WorldCfg.DailyQuestResetTimeHour].Int32, true);
        }

        public void DailyReset()
        {
            // reset all saved quest status
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_RESET_CHARACTER_QUESTSTATUS_DAILY);
            DB.Characters.Execute(stmt);
                        
            // reset all quest status in memory
            foreach (var itr in m_sessions)
            {
                Player player = itr.Value.GetPlayer();
                if (player != null)
                    player.DailyReset();
            }

            // reselect pools
            Global.QuestPoolMgr.ChangeDailyQuests();

            // Update faction balance
            UpdateWarModeRewardValues();

            // store next reset time
            RealmTime nowLocal = LoopTime.RealmTime;
            RealmTime next = GetNextDailyResetTime(nowLocal);
            Cypher.Assert(nowLocal < next);

            m_NextDailyQuestReset = next;
            SetPersistentWorldVariable(NextDailyQuestResetTimeVarId, (UnixTime)next);

            Log.outInfo(LogFilter.Misc, "Daily quests for all characters have been reset.");
        }

        static RealmTime GetNextWeeklyResetTime(RealmTime localTime)
        {
            localTime = GetNextDailyResetTime(localTime);
            DayOfWeek wday = localTime.DayOfWeek;
            DayOfWeek target = (DayOfWeek)WorldConfig.Values[WorldCfg.WeeklyQuestResetTimeWDay].Int32;
            if (target < wday)
                wday -= 7;

            return localTime + (Days)(target - wday);
        }

        public void ResetWeeklyQuests()
        {
            // reset all saved quest status
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_RESET_CHARACTER_QUESTSTATUS_WEEKLY);
            DB.Characters.Execute(stmt);
            // reset all quest status in memory
            foreach (var itr in m_sessions)
            {
                Player player = itr.Value.GetPlayer();
                if (player != null)
                    player.ResetWeeklyQuestStatus();
            }

            // reselect pools
            Global.QuestPoolMgr.ChangeWeeklyQuests();

            // store next reset time
            RealmTime nowLocal = LoopTime.RealmTime;
            RealmTime next = GetNextWeeklyResetTime(nowLocal);
            Cypher.Assert(nowLocal < next);

            m_NextWeeklyQuestReset = next;
            SetPersistentWorldVariable(NextWeeklyQuestResetTimeVarId, (UnixTime)next);

            Log.outInfo(LogFilter.Misc, "Weekly quests for all characters have been reset.");
        }

        static RealmTime GetNextMonthlyResetTime(RealmTime localTime)
        {
            localTime = GetNextDailyResetTime(localTime);

            // It should be first day of month
            if (localTime.Day == 1)
                return localTime;

            // It will be next month
            // (may jump to the next year, so let's let DateTime calculate it for us)
            localTime += (Days)30;

            // It must be '1' day of month                                  *
            return (RealmTime)new DateTime(localTime.Year, localTime.Month, 1, localTime.Hour, localTime.Minute, 0, localTime.Kind);
        }

        public void ResetMonthlyQuests()
        {
            // reset all saved quest status
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_RESET_CHARACTER_QUESTSTATUS_MONTHLY);
            DB.Characters.Execute(stmt);
            // reset all quest status in memory
            foreach (var itr in m_sessions)
            {
                Player player = itr.Value.GetPlayer();
                if (player != null)
                    player.ResetMonthlyQuestStatus();
            }

            // reselect pools
            Global.QuestPoolMgr.ChangeMonthlyQuests();

            // store next reset time
            RealmTime nowLocal = LoopTime.RealmTime;
            RealmTime next = GetNextMonthlyResetTime(nowLocal);
            Cypher.Assert(nowLocal < next);

            m_NextMonthlyQuestReset = next;
            SetPersistentWorldVariable(NextMonthlyQuestResetTimeVarId, (UnixTime)next);

            Log.outInfo(LogFilter.Misc, "Monthly quests for all characters have been reset.");
        }

        void CheckScheduledResetTimes()
        {
            RealmTime now = LoopTime.RealmTime;
            if (m_NextDailyQuestReset <= now)
                DailyReset();
            if (m_NextWeeklyQuestReset <= now)
                ResetWeeklyQuests();
            if (m_NextMonthlyQuestReset <= now)
                ResetMonthlyQuests();
        }

        void InitRandomBGResetTime()
        {
            RealmTime bgtime = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextBGRandomDailyResetTimeVarId);
            if (bgtime == RealmTime.Zero)
                m_NextRandomBGReset = LoopTime.RealmTime;         // game time not yet init

            // generate time by config
            RealmTime curTime = LoopTime.RealmTime;

            // current day reset time
            RealmTime nextDayResetTime = GetNextResetTime(WorldConfig.Values[WorldCfg.RandomBgResetHour].Int32);

            // next reset time before current moment
            if (curTime >= nextDayResetTime)
                nextDayResetTime += (Days)1;

            // normalize reset time
            m_NextRandomBGReset = bgtime < curTime ? nextDayResetTime - (Days)1 : nextDayResetTime;

            if (bgtime == RealmTime.Zero)
                SetPersistentWorldVariable(NextBGRandomDailyResetTimeVarId, (UnixTime)m_NextRandomBGReset);
        }

        void InitCalendarOldEventsDeletionTime()
        {
            RealmTime nowLocal = LoopTime.RealmTime;
            RealmTime nextDeletionTime = (RealmTime)GetHourTimestamp(nowLocal, WorldConfig.Values[WorldCfg.CalendarDeleteOldEventsHour].Int32);
            RealmTime currentDeletionTime = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextOldCalendarEventDeletionTimeVarId);

            // If the reset time saved in the worldstate is before now it means the server was offline when the reset was supposed to occur.
            // In this case we set the reset time in the past and next world update will do the reset and schedule next one in the future.
            if (currentDeletionTime < nowLocal)
                m_NextCalendarOldEventsDeletionTime = nextDeletionTime - (Days)1;
            else
                m_NextCalendarOldEventsDeletionTime = nextDeletionTime;

            if (currentDeletionTime == RealmTime.Zero)
                SetPersistentWorldVariable(NextOldCalendarEventDeletionTimeVarId, (UnixTime)m_NextCalendarOldEventsDeletionTime);
        }

        void InitGuildResetTime()
        {
            RealmTime gtime = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextGuildDailyResetTimeVarId);
            if (gtime == RealmTime.Zero)
                m_NextGuildReset = LoopTime.RealmTime;         // game time not yet init

            RealmTime curTime = LoopTime.RealmTime;
            RealmTime nextDayResetTime = GetNextResetTime(WorldConfig.Values[WorldCfg.GuildResetHour].Int32);

            if (curTime >= nextDayResetTime)
                nextDayResetTime += (Days)1;

            // normalize reset time
            m_NextGuildReset = gtime < curTime ? nextDayResetTime - (Days)1 : nextDayResetTime;

            if (gtime == RealmTime.Zero)
                SetPersistentWorldVariable(NextGuildDailyResetTimeVarId, (UnixTime)m_NextGuildReset);
        }

        void InitCurrencyResetTime()
        {
            RealmTime currencytime = (RealmTime)(UnixTime)GetPersistentWorldVariable(NextCurrencyResetTimeVarId);
            if (currencytime == RealmTime.Zero)
                m_NextCurrencyReset = LoopTime.RealmTime;         // game time not yet init

            // generate time by config
            RealmTime curTime = LoopTime.RealmTime;

            RealmTime nextWeekResetTime = GetNextResetTime(WorldConfig.Values[WorldCfg.CurrencyResetDay].Int32, WorldConfig.Values[WorldCfg.CurrencyResetHour].Int32);

            TimeSpan currencyResetInterval = WorldConfig.Values[WorldCfg.CurrencyResetInterval].TimeSpan;

            // next reset time before current moment
            if (curTime >= nextWeekResetTime)
                nextWeekResetTime += currencyResetInterval;

            // normalize reset time
            m_NextCurrencyReset = currencytime < curTime ? nextWeekResetTime - currencyResetInterval : nextWeekResetTime;

            if (currencytime == RealmTime.Zero)
                SetPersistentWorldVariable(NextCurrencyResetTimeVarId, (UnixTime)m_NextCurrencyReset);
        }

        void ResetCurrencyWeekCap()
        {
            DB.Characters.Execute("UPDATE `character_currency` SET `WeeklyQuantity` = 0");

            foreach (var session in m_sessions.Values)
            {
                if (session.GetPlayer() != null)
                    session.GetPlayer().ResetCurrencyWeekCap();
            }

            m_NextCurrencyReset += WorldConfig.Values[WorldCfg.CurrencyResetInterval].TimeSpan;
            SetPersistentWorldVariable(NextCurrencyResetTimeVarId, (UnixTime)m_NextCurrencyReset);
        }

        public void ResetEventSeasonalQuests(ushort event_id, RealmTime eventStartTime)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_RESET_CHARACTER_QUESTSTATUS_SEASONAL_BY_EVENT);
            stmt.SetUInt16(0, event_id);
            stmt.SetInt64(1, (UnixTime64)eventStartTime);
            DB.Characters.Execute(stmt);

            foreach (var session in m_sessions.Values)
                session.GetPlayer()?.ResetSeasonalQuestStatus(event_id, eventStartTime);
        }

        void ResetRandomBG()
        {
            Log.outInfo(LogFilter.Server, "Random BG status reset for all characters.");

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_BATTLEGROUND_RANDOM_ALL);
            DB.Characters.Execute(stmt);

            foreach (var session in m_sessions.Values)
                if (session.GetPlayer() != null)
                    session.GetPlayer().SetRandomWinner(false);

            m_NextRandomBGReset += (Days)1;
            SetPersistentWorldVariable(NextBGRandomDailyResetTimeVarId, (UnixTime)m_NextRandomBGReset);
        }

        void CalendarDeleteOldEvents()
        {
            Log.outInfo(LogFilter.Misc, "Calendar deletion of old events.");

            m_NextCalendarOldEventsDeletionTime += (Days)1;
            SetPersistentWorldVariable(NextOldCalendarEventDeletionTimeVarId, (UnixTime)m_NextCalendarOldEventsDeletionTime);
            Global.CalendarMgr.DeleteOldEvents();
        }

        void ResetGuildCap()
        {
            m_NextGuildReset += (Days)1;
            SetPersistentWorldVariable(NextGuildDailyResetTimeVarId, (UnixTime)m_NextGuildReset);
            int week = GetPersistentWorldVariable(NextGuildWeeklyResetTimeVarId);
            week = week < 7 ? week + 1 : 1;

            Log.outInfo(LogFilter.Server, $"Guild Daily Cap reset. Week: {week == 1}");
            SetPersistentWorldVariable(NextGuildWeeklyResetTimeVarId, week);
            Global.GuildMgr.ResetTimes(week == 1);
        }

        void UpdateMaxSessionCounters()
        {
            m_maxActiveSessionCount = Math.Max(m_maxActiveSessionCount, m_sessions.Count - m_QueuedPlayer.Count);
            m_maxQueuedSessionCount = Math.Max(m_maxQueuedSessionCount, m_QueuedPlayer.Count);
        }

        public string GetDBVersion() { return m_DBVersion; }

        public void LoadDBVersion()
        {
            m_DBVersion = "Unknown world database.";

            SQLResult result = DB.World.Query("SELECT db_version, cache_id FROM version LIMIT 1");
            if (!result.IsEmpty())
            {
                m_DBVersion = result.Read<string>(0);
                // will be overwrite by config values if different and non-0
                WorldConfig.SetValue(WorldCfg.ClientCacheVersion, result.Read<uint>(1));
            }
        }

        void UpdateAreaDependentAuras()
        {
            foreach (var session in m_sessions.Values)
            {
                if (session.GetPlayer() != null && session.GetPlayer().IsInWorld)
                {
                    session.GetPlayer().UpdateAreaDependentAuras(session.GetPlayer().GetAreaId());
                    session.GetPlayer().UpdateZoneDependentAuras(session.GetPlayer().GetZoneId());
                }
            }
        }

        public bool IsBattlePetJournalLockAcquired(ObjectGuid battlenetAccountGuid)
        {
            foreach (var sessionForBnet in m_sessionsByBnetGuid.LookupByKey(battlenetAccountGuid))
            {
                if (sessionForBnet.GetBattlePetMgr().HasJournalLock())
                    return true;
            }

            return false;
        }

        public int GetPersistentWorldVariable(string var)
        {
            return m_worldVariables.LookupByKey(var);
        }

        public void SetPersistentWorldVariable(string var, int value)
        {
            m_worldVariables[var] = value;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_WORLD_VARIABLE);
            stmt.SetString(0, var);
            stmt.SetInt32(1, value);
            DB.Characters.Execute(stmt);
        }

        void LoadPersistentWorldVariables()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            SQLResult result = DB.Characters.Query("SELECT ID, Value FROM world_variable");
            if (!result.IsEmpty())
            {
                do
                {
                    m_worldVariables[result.Read<string>(0)] = result.Read<int>(1);
                } while (result.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {m_worldVariables.Count} world variables in {Time.Diff(oldMSTime)} ms.");
        }

        void ProcessQueryCallbacks()
        {
            _queryProcessor.ProcessReadyCallbacks();
        }

        public void ReloadRBAC()
        {
            // Passive reload, we mark the data as invalidated and next time a permission is checked it will be reloaded
            Log.outInfo(LogFilter.Rbac, "World.ReloadRBAC()");
            foreach (var session in m_sessions.Values)
                session.InvalidateRBACData();
        }

        public List<WorldSession> GetAllSessions()
        {
            return m_sessions.Values.ToList();
        }

        public int GetActiveAndQueuedSessionCount() { return m_sessions.Count; }
        public int GetActiveSessionCount() { return m_sessions.Count - m_QueuedPlayer.Count; }
        public int GetQueuedSessionCount() { return m_QueuedPlayer.Count; }
        // Get the maximum number of parallel sessions on the server since last reboot
        public int GetMaxQueuedSessionCount() { return m_maxQueuedSessionCount; }
        public int GetMaxActiveSessionCount() { return m_maxActiveSessionCount; }

        public int GetPlayerCount() { return m_PlayerCount; }
        public int GetMaxPlayerCount() { return m_MaxPlayerCount; }

        public void IncreasePlayerCount()
        {
            m_PlayerCount++;
            m_MaxPlayerCount = Math.Max(m_MaxPlayerCount, m_PlayerCount);
        }
        public void DecreasePlayerCount() { m_PlayerCount--; }

        public AccountTypes GetPlayerSecurityLimit() { return m_allowedSecurityLevel; }

        public void SetPlayerAmountLimit(int limit) { m_playerLimit = limit; }
        public int GetPlayerAmountLimit() { return m_playerLimit; }

        /// Get the path where data (dbc, maps) are stored on disk
        public string GetDataPath() { return _dataPath; }

        public void SetDataPath(string path) { _dataPath = path; }

        public RealmTime GetNextDailyQuestsResetTime() { return m_NextDailyQuestReset; }
        public void SetNextDailyQuestsResetTime(RealmTime time) { m_NextDailyQuestReset = time; }
        public RealmTime GetNextWeeklyQuestsResetTime() { return m_NextWeeklyQuestReset; }
        public void SetNextWeeklyQuestsResetTime(RealmTime time) { m_NextWeeklyQuestReset = time; }
        public RealmTime GetNextMonthlyQuestsResetTime() { return m_NextMonthlyQuestReset; }
        public void SetNextMonthlyQuestsResetTime(RealmTime time) { m_NextMonthlyQuestReset = time; }

        RealmTime GetNextRandomBGResetTime() { return m_NextRandomBGReset; }

        private static RealmTime GetNextResetTime(int hours)
        {
            return LoopTime.RealmTime.Date + (Hours)hours;
        }

        private static RealmTime GetNextResetTime(int days, int hours)
        {
            return LoopTime.RealmTime.Date + new TimeSpan(days, hours, 0, 0);
        }

        private static RealmTime GetNextResetTime(int months, int days, int hours)
        {
            return new(LoopTime.RealmTime.Date.Data.AddMonths(months).Add(new(days, hours, 0)));
        }

        public static DateTime GetHourTimestamp(DateTime time, int hour, bool onlyAfterTime = true)
        {
            DateTime nextTime = time.Date + (Hours)hour;

            if (onlyAfterTime && (nextTime <= time))
                return nextTime + (Days)1;

            return nextTime;
        }

        public uint GetConfigMaxSkillValue()
        {
            int lvl = WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32;
            return (uint)(lvl > 60 ? 300 + ((lvl - 60) * 75) / 10 : lvl * 5);
        }

        public bool IsShuttingDown => m_ShutdownTime > ServerTime.Zero;
        public TimeSpan GetShutDownTimeLeft() { return m_ShutdownTime - LoopTime.ServerTime; }

        public int GetExitCode() { return (int)m_ExitCode; }

        public void StopNow(ShutdownExitCode exitcode = ShutdownExitCode.Error) { IsStopped = true; m_ExitCode = exitcode; }

        public bool IsPvPRealm()
        {
            RealmType realmtype = (RealmType)WorldConfig.Values[WorldCfg.GameType].Int32;
            return (realmtype == RealmType.PVP
                || realmtype == RealmType.RPPVP
                || realmtype == RealmType.FFAPVP);
        }

        public bool IsFFAPvPRealm()
        {
            return WorldConfig.Values[WorldCfg.GameType].Int32 == (int)RealmType.FFAPVP;
        }

        public Locale GetDefaultDbcLocale() { return m_defaultDbcLocale; }        

        public Realm GetRealm() { return _realm; }
        public RealmId GetRealmId() { return _realm.Id; }

        public void RemoveOldCorpses()
        {
            m_timers[WorldTimers.Corpses].SetCurrent(m_timers[WorldTimers.Corpses].GetInterval());
        }

        void UpdateWarModeRewardValues()
        {
            long[] warModeEnabledFaction = new long[2];

            // Search for characters that have war mode enabled and played during the last week
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_WAR_MODE_TUNING);
            stmt.SetUInt32(0, (uint)PlayerFlags.WarModeDesired);
            stmt.SetUInt32(1, (uint)PlayerFlags.WarModeDesired);

            SQLResult result = DB.Characters.Query(stmt);
            if (!result.IsEmpty())
            {
                do
                {
                    byte race = result.Read<byte>(0);

                    var raceEntry = CliDB.ChrRacesStorage.LookupByKey(race);
                    if (raceEntry != null)
                    {
                        var raceFaction = CliDB.FactionTemplateStorage.LookupByKey(raceEntry.FactionID);
                        if (raceFaction != null)
                        {
                            if ((raceFaction.FactionGroup & (byte)FactionMasks.Alliance) != 0)
                                warModeEnabledFaction[BattleGroundTeamId.Alliance] += result.Read<long>(1);
                            else if ((raceFaction.FactionGroup & (byte)FactionMasks.Horde) != 0)
                                warModeEnabledFaction[BattleGroundTeamId.Horde] += result.Read<long>(1);
                        }
                    }

                } while (result.NextRow());
            }


            int dominantFaction = BattleGroundTeamId.Alliance;
            int outnumberedFactionReward = 0;

            if (warModeEnabledFaction.Any(val => val != 0))
            {
                long dominantFactionCount = warModeEnabledFaction[BattleGroundTeamId.Alliance];
                if (warModeEnabledFaction[BattleGroundTeamId.Alliance] < warModeEnabledFaction[BattleGroundTeamId.Horde])
                {
                    dominantFactionCount = warModeEnabledFaction[BattleGroundTeamId.Horde];
                    dominantFaction = BattleGroundTeamId.Horde;
                }

                double total = warModeEnabledFaction[BattleGroundTeamId.Alliance] + warModeEnabledFaction[BattleGroundTeamId.Horde];
                double pct = dominantFactionCount / total;

                if (pct >= WorldConfig.Values[WorldCfg.CallToArms20Pct].Float)
                    outnumberedFactionReward = 20;
                else if (pct >= WorldConfig.Values[WorldCfg.CallToArms10Pct].Float)
                    outnumberedFactionReward = 10;
                else if (pct >= WorldConfig.Values[WorldCfg.CallToArms5Pct].Float)
                    outnumberedFactionReward = 5;
            }

            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.WarModeHordeBuffValue, 
                10 + (dominantFaction == BattleGroundTeamId.Alliance ? outnumberedFactionReward : 0), false, null);
            Global.WorldStateMgr.SetValueAndSaveInDb(WorldStates.WarModeAllianceBuffValue, 
                10 + (dominantFaction == BattleGroundTeamId.Horde ? outnumberedFactionReward : 0), false, null);
        }

        public uint GetVirtualRealmAddress()
        {
            return _realm.Id.GetAddress();
        }

        public float GetMaxVisibleDistanceOnContinents() { return m_MaxVisibleDistanceOnContinents; }
        public float GetMaxVisibleDistanceInInstances() { return m_MaxVisibleDistanceInInstances; }
        public float GetMaxVisibleDistanceInBG() { return m_MaxVisibleDistanceInBG; }
        public float GetMaxVisibleDistanceInArenas() { return m_MaxVisibleDistanceInArenas; }

        public Milliseconds GetVisibilityNotifyPeriodOnContinents() { return m_visibility_notify_periodOnContinents; }
        public Milliseconds GetVisibilityNotifyPeriodInInstances() { return m_visibility_notify_periodInInstances; }
        public Milliseconds GetVisibilityNotifyPeriodInBG() { return m_visibility_notify_periodInBG; }
        public Milliseconds GetVisibilityNotifyPeriodInArenas() { return m_visibility_notify_periodInArenas; }

        public Locale GetAvailableDbcLocale(Locale locale)
        {
            if (m_availableDbcLocaleMask[(int)locale])
                return locale;
            else
                return m_defaultDbcLocale;
        }

        public CleaningFlags GetCleaningFlags() { return m_CleaningFlags; }
        public void SetCleaningFlags(CleaningFlags flags) { m_CleaningFlags = flags; }

        public bool IsGuidWarning() { return _guidWarn; }
        public bool IsGuidAlert() { return _guidAlert; }

        public WorldUpdateTime GetWorldUpdateTime() { return _worldUpdateTime; }

        #region Fields
        ServerTime m_ShutdownTime;
        ServerTime m_lastShutdownMessageTime;
        ShutdownMask m_ShutdownMask;
        ShutdownExitCode m_ExitCode;
        public bool IsStopped;

        Dictionary<byte, Autobroadcast> m_Autobroadcasts = new();

        CleaningFlags m_CleaningFlags;

        float m_MaxVisibleDistanceOnContinents = SharedConst.DefaultVisibilityDistance;
        float m_MaxVisibleDistanceInInstances = SharedConst.DefaultVisibilityInstance;
        float m_MaxVisibleDistanceInBG = SharedConst.DefaultVisibilityBGAreans;
        float m_MaxVisibleDistanceInArenas = SharedConst.DefaultVisibilityBGAreans;

        Milliseconds m_visibility_notify_periodOnContinents = SharedConst.DefaultVisibilityNotifyPeriod;
        Milliseconds m_visibility_notify_periodInInstances = SharedConst.DefaultVisibilityNotifyPeriod;
        Milliseconds m_visibility_notify_periodInBG = SharedConst.DefaultVisibilityNotifyPeriod;
        Milliseconds m_visibility_notify_periodInArenas = SharedConst.DefaultVisibilityNotifyPeriod;

        bool m_isClosed;

        Dictionary<WorldTimers, IntervalTimer> m_timers = new();
        RealmTime mailExpiresTime;
        ServerTime blackmarketNextRefreshTime;

        ConcurrentDictionary<int, WorldSession> m_sessions = new();
        MultiMap<ObjectGuid, WorldSession> m_sessionsByBnetGuid = new();
        Dictionary<int, ServerTime> m_disconnects = new();
        int m_maxActiveSessionCount;
        int m_maxQueuedSessionCount;
        int m_PlayerCount;
        int m_MaxPlayerCount;

        Dictionary<string, int> m_worldVariables = new();
        int m_playerLimit;
        AccountTypes m_allowedSecurityLevel;
        Locale m_defaultDbcLocale;                     // from config for one from loaded DBC locales
        BitSet m_availableDbcLocaleMask;                       // by loaded DBC
        List<string> m_motd = new();

        // scheduled reset times
        RealmTime m_NextDailyQuestReset;
        RealmTime m_NextWeeklyQuestReset;
        RealmTime m_NextMonthlyQuestReset;
        RealmTime m_NextRandomBGReset;
        RealmTime m_NextCalendarOldEventsDeletionTime;
        RealmTime m_NextGuildReset;
        RealmTime m_NextCurrencyReset;

        List<WorldSession> m_QueuedPlayer = new();
        ConcurrentQueue<WorldSession> addSessQueue = new();

        ConcurrentQueue<Tuple<WorldSocket, long>> _linkSocketQueue = new();

        AsyncCallbackProcessor<QueryCallback> _queryProcessor = new();

        Realm _realm;

        string _dataPath;
        string m_DBVersion;

        WorldUpdateTime _worldUpdateTime;

        string _guidWarningMsg;
        string _alertRestartReason;

        object _guidAlertLock = new();

        bool _guidWarn;
        bool _guidAlert;
        TimeSpan _warnDiff;
        ServerTime _warnShutdownTime;
        #endregion
    }

    /// Timers for different object refresh rates
    public enum WorldTimers
    {
        Auctions,
        AuctionsPending,
        UpTime,
        Corpses,
        Events,
        CleanDB,
        AutoBroadcast,
        MailBox,
        DeleteChars,
        AhBot,
        PingDB,
        GuildSave,
        Blackmarket,
        WhoList,
        ChannelSave,
        Max
    }

    public enum ServerMessageType
    {
        ShutdownTime = 1,
        RestartTime = 2,
        String = 3,
        ShutdownCancelled = 4,
        RestartCancelled = 5,
        BgShutdownTime = 6,
        BgRestartTime = 7,
        InstanceShutdownTime = 8,
        InstanceRestartTime = 9,
        ContentReady = 10,
        TicketServicedSoon = 11,
        WaitTimeUnavailable = 12,
        TicketWaitTime = 13,
    }

    [Flags]
    public enum ShutdownMask
    {
        Restart = 1,
        Idle = 2,
        Force = 4
    }

    public enum ShutdownExitCode
    {
        Shutdown = 0,
        Error = 1,
        Restart = 2,
    }

    public class WorldWorldTextBuilder : MessageBuilder
    {
        public WorldWorldTextBuilder(uint textId, params object[] args)
        {
            i_textId = textId;
            i_args = args;
        }

        public override MultiplePacketSender Invoke(Locale locale)
        {
            string text = Global.ObjectMgr.GetCypherString(i_textId, locale);

            if (i_args != null)
                text = string.Format(text, i_args);

            MultiplePacketSender sender = new();

            var lines = new StringArray(text, "\n");
            for (var i = 0; i < lines.Length; ++i)
            {
                ChatPkt messageChat = new();
                messageChat.Initialize(ChatMsg.System, Language.Universal, null, null, lines[i]);
                messageChat.Write();
                sender.Packets.Add(messageChat);
            }

            return sender;
        }

        uint i_textId;
        object[] i_args;

        public class MultiplePacketSender : IDoWork<Player>
        {
            public void Invoke(Player receiver)
            {
                foreach (var packet in Packets)
                    receiver.SendPacket(packet);
            }

            public List<ServerPacket> Packets = new();
        }
    }

    struct Autobroadcast
    {
        public Autobroadcast(string message, byte weight)
        {
            Message = message;
            Weight = weight;
        }

        public string Message;
        public byte Weight;
    }
}
