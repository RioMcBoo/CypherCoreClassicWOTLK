﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.AI;
using Game.BattleFields;
using Game.BattleGrounds;
using Game.Chat;
using Game.Conditions;
using Game.DataStorage;
using Game.Entities;
using Game.Groups;
using Game.Guilds;
using Game.Maps;
using Game.Movement;
using Game.PvP;
using Game.Spells;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;

namespace Game.Scripting
{
    // Manages registration, loading, and execution of Scripts.
    public class ScriptManager : Singleton<ScriptManager>
    {
        ScriptManager() { }

        //Initialization
        public void Initialize()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            LoadDatabase();

            Log.outInfo(LogFilter.ServerLoading, "Loading C# scripts");

            FillSpellSummary();

            //Load Scripts.dll
            LoadScripts();

            // MapScripts
            Global.MapMgr.AddSC_BuiltInScripts();

            Log.outInfo(LogFilter.ServerLoading, 
                $"Loaded {GetScriptCount()} C# scripts in {Time.Diff(oldMSTime)} ms.");
        }

        public void LoadScripts()
        {
            if (!File.Exists(AppContext.BaseDirectory + "Scripts.dll"))
            {
                Log.outError(LogFilter.ServerLoading, 
                    "Cant find Scripts.dll, Only Core Scripts are loaded.");
                return;
            }

            Assembly assembly = Assembly.LoadFile(AppContext.BaseDirectory + "Scripts.dll");
            if (assembly == null)
            {
                Log.outError(LogFilter.ServerLoading, 
                    "Error Loading Scripts.dll, Only Core Scripts are loaded.");
                return;
            }

            foreach (var type in assembly.GetTypes())
            {
                var attributes = (ScriptAttribute[])type.GetCustomAttributes<ScriptAttribute>();
                if (!attributes.Empty())
                {
                    var constructors = type.GetConstructors();
                    if (constructors.Length == 0)
                    {
                        Log.outError(LogFilter.Scripts, 
                            $"Script: {type.Name} contains no Public Constructors. Can't load script.");
                        continue;
                    }

                    foreach (var attribute in attributes)
                    {
                        var genericType = type;
                        string name = type.Name;

                        bool validArgs = true;
                        int i = 0;
                        foreach (var constructor in constructors)
                        {
                            var parameters = constructor.GetParameters();
                            if (parameters.Length != attribute.Args.Length)
                                continue;

                            foreach (var arg in constructor.GetParameters())
                            {
                                if (arg.ParameterType != attribute.Args[i++].GetType())
                                {
                                    validArgs = false;
                                    break;
                                }
                            }

                            if (validArgs)
                                break;
                        }

                        if (!validArgs)
                        {
                            Log.outError(LogFilter.Scripts, 
                                $"Script: {type.Name} contains no Public Constructors " +
                                $"with the right parameter types. Can't load script.");
                            continue;
                        }

                        switch (type.BaseType.Name)
                        {
                            case nameof(SpellScript):
                                genericType = typeof(GenericSpellScriptLoader<>).MakeGenericType(type);
                                name = name.Replace("_SpellScript", "");
                                break;
                            case nameof(AuraScript):
                                genericType = typeof(GenericAuraScriptLoader<>).MakeGenericType(type);
                                name = name.Replace("_AuraScript", "");
                                break;
                            case nameof(GameObjectAI):
                                genericType = typeof(GenericGameObjectScript<>).MakeGenericType(type);
                                break;
                            case nameof(AreaTriggerAI):
                                genericType = typeof(GenericAreaTriggerScript<>).MakeGenericType(type);
                                break;
                            case "SpellScriptLoader":
                            case "AuraScriptLoader":
                            case "WorldScript":
                            case "FormulaScript":
                            case "WorldMapScript":
                            case "InstanceMapScript":
                            case "BattlegroundMapScript":
                            case "ItemScript":
                            case "UnitScript":
                            case "CreatureScript":
                            case "GameObjectScript":
                            case "AreaTriggerScript":
                            case "OutdoorPvPScript":
                            case "WeatherScript":
                            case "AuctionHouseScript":
                            case "ConditionScript":
                            case "VehicleScript":
                            case "DynamicObjectScript":
                            case "TransportScript":
                            case "AchievementCriteriaScript":
                            case "PlayerScript":
                            case "AccountScript":
                            case "GuildScript":
                            case "GroupScript":
                            case "AreaTriggerEntityScript":
                            case "OnlyOnceAreaTriggerScript":
                            case "SceneScript":
                            case "QuestScript":
                            case "ConversationScript":
                            case "AchievementScript":
                            case "BattlefieldScript":
                            case "EventScript":
                                if (!attribute.Name.IsEmpty())
                                    name = attribute.Name;

                                if (attribute.Args.Empty())
                                    Activator.CreateInstance(genericType);
                                else
                                    Activator.CreateInstance(genericType, new object[] { name }.Combine(attribute.Args));
                                continue;
                            default:
                                genericType = typeof(GenericCreatureScript<>).MakeGenericType(type);
                                break;
                        }

                        if (!attribute.Name.IsEmpty())
                            name = attribute.Name;

                        Activator.CreateInstance(genericType, name, attribute.Args);
                    }
                }
            }
        }

        public void LoadDatabase()
        {
            LoadScriptSplineChains();
        }

        void LoadScriptSplineChains()
        {
            RelativeTime oldMSTime = Time.NowRelative;

            m_mSplineChainsMap.Clear();

            //                                             0      1        2         3                 4            5
            SQLResult resultMeta = DB.World.Query("SELECT entry, chainId, splineId, expectedDuration, msUntilNext, velocity FROM script_spline_chain_meta ORDER BY entry asc, chainId asc, splineId asc");
            //                                           0      1        2         3    4  5  6
            SQLResult resultWP = DB.World.Query("SELECT entry, chainId, splineId, wpId, x, y, z FROM script_spline_chain_waypoints ORDER BY entry asc, chainId asc, splineId asc, wpId asc");
            if (resultMeta.IsEmpty() || resultWP.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, 
                    "Loaded spline chain data for 0 chains, consisting of 0 splines with 0 waypoints. " +
                    "DB tables `script_spline_chain_meta` and `script_spline_chain_waypoints` are empty.");
            }
            else
            {
                uint chainCount = 0, splineCount = 0, wpCount = 0;
                do
                {
                    int entry = resultMeta.Read<int>(0);
                    ushort chainId = resultMeta.Read<ushort>(1);
                    byte splineId = resultMeta.Read<byte>(2);

                    var chain = m_mSplineChainsMap[(entry, chainId)];
                    if (splineId != chain.Count)
                    {
                        Log.outWarn(LogFilter.ServerLoading, 
                            $"Creature #{entry}: Chain {chainId} has orphaned spline {splineId}, skipped.");
                        continue;
                    }

                    Milliseconds expectedDuration = (Milliseconds)resultMeta.Read<int>(3);
                    Milliseconds msUntilNext = (Milliseconds)resultMeta.Read<int>(4);
                    Speed velocity = (Speed)resultMeta.Read<float>(5);
                    m_mSplineChainsMap.Add((entry, chainId), new SplineChainLink(expectedDuration, msUntilNext, velocity));

                    if (splineId == 0)
                        ++chainCount;
                    ++splineCount;
                } while (resultMeta.NextRow());

                do
                {
                    int entry = resultWP.Read<int>(0);
                    ushort chainId = resultWP.Read<ushort>(1);
                    byte splineId = resultWP.Read<byte>(2);
                    byte wpId = resultWP.Read<byte>(3);
                    float posX = resultWP.Read<float>(4);
                    float posY = resultWP.Read<float>(5);
                    float posZ = resultWP.Read<float>(6);
                    var chain = m_mSplineChainsMap[(entry, chainId)];
                    if (chain.Empty())
                    {
                        Log.outWarn(LogFilter.ServerLoading, 
                            $"Creature #{entry} has waypoint data for spline chain {chainId}. " +
                            $"No such chain exists - entry skipped.");
                        continue;
                    }

                    if (splineId >= chain.Count)
                    {
                        Log.outWarn(LogFilter.ServerLoading, 
                            $"Creature #{entry} has waypoint data for spline ({chainId},{splineId}). " +
                            $"The specified chain does not have a spline with this index - entry skipped.");
                        continue;
                    }
                    SplineChainLink spline = chain[splineId];
                    if (wpId != spline.Points.Count)
                    {
                        Log.outWarn(LogFilter.ServerLoading, 
                            $"Creature #{entry} has orphaned waypoint data in spline " +
                            $"({chainId},{splineId}) at index {wpId}. Skipped.");
                        continue;
                    }
                    spline.Points.Add(new Vector3(posX, posY, posZ));
                    ++wpCount;
                } while (resultWP.NextRow());

                Log.outInfo(LogFilter.ServerLoading, 
                    $"Loaded spline chain data for {chainCount} chains, " +
                    $"consisting of {splineCount} splines with {wpCount} waypoints in {Time.Diff(oldMSTime)} ms.");
            }
        }

        public void FillSpellSummary()
        {
            UnitAI.FillAISpellInfo();
        }

        public IReadOnlyList<SplineChainLink> GetSplineChain(Creature who, ushort chainId)
        {
            return GetSplineChain(who.GetEntry(), chainId);
        }

        IReadOnlyList<SplineChainLink> GetSplineChain(int entry, ushort chainId)
        {
            return m_mSplineChainsMap[(entry, chainId)];
        }

        public string ScriptsVersion() { return "Integrated Cypher Scripts"; }

        public void IncrementScriptCount() { ++_ScriptCount; }
        public uint GetScriptCount() { return _ScriptCount; }

        //Unloading
        public void Unload()
        {
            foreach (DictionaryEntry entry in ScriptStorage)
            {
                IScriptRegistry scriptRegistry = (IScriptRegistry)entry.Value;
                scriptRegistry.Unload();
            }

            ScriptStorage.Clear();
        }

        //SpellScriptLoader
        public List<SpellScript> CreateSpellScripts(int spellId, Spell invoker)
        {
            var scriptList = new List<SpellScript>();
            var bounds = Global.ObjectMgr.GetSpellScriptsBounds(spellId);

            var reg = GetScriptRegistry<SpellScriptLoader>();
            if (reg == null)
                return scriptList;

            foreach (var id in bounds)
            {
                var tmpscript = reg.GetScriptById(id);
                if (tmpscript == null)
                    continue;

                SpellScript script = tmpscript.GetSpellScript();
                if (script == null)
                    continue;

                script._Init(tmpscript.GetName(), spellId);
                if (!script._Load(invoker))
                    continue;

                scriptList.Add(script);
            }

            return scriptList;
        }

        public List<AuraScript> CreateAuraScripts(int spellId, Aura invoker)
        {
            var scriptList = new List<AuraScript>();
            var bounds = Global.ObjectMgr.GetSpellScriptsBounds(spellId);

            var reg = GetScriptRegistry<AuraScriptLoader>();
            if (reg == null)
                return scriptList;

            foreach (var id in bounds)
            {
                var tmpscript = reg.GetScriptById(id);
                if (tmpscript == null)
                    continue;

                AuraScript script = tmpscript.GetAuraScript();
                if (script == null)
                    continue;

                script._Init(tmpscript.GetName(), spellId);
                if (!script._Load(invoker))
                    continue;

                scriptList.Add(script);
            }

            return scriptList;
        }

        public Dictionary<SpellScriptLoader, int> CreateSpellScriptLoaders(int spellId)
        {
            var scriptDic = new Dictionary<SpellScriptLoader, int>();
            var bounds = Global.ObjectMgr.GetSpellScriptsBounds(spellId);

            var reg = GetScriptRegistry<SpellScriptLoader>();
            if (reg == null)
                return scriptDic;

            foreach (var id in bounds)
            {
                var tmpscript = reg.GetScriptById(id);
                if (tmpscript == null)
                    continue;

                scriptDic.Add(tmpscript, id);
            }

            return scriptDic;
        }

        public Dictionary<AuraScriptLoader, int> CreateAuraScriptLoaders(int spellId)
        {
            var scriptDic = new Dictionary<AuraScriptLoader, int>();
            var bounds = Global.ObjectMgr.GetSpellScriptsBounds(spellId);

            var reg = GetScriptRegistry<AuraScriptLoader>();
            if (reg == null)
                return scriptDic;

            foreach (var id in bounds)
            {
                var tmpscript = reg.GetScriptById(id);
                if (tmpscript == null)
                    continue;

                scriptDic.Add(tmpscript, id);
            }

            return scriptDic;
        }

        //WorldScript
        public void OnOpenStateChange(bool open)
        {
            ForEach<WorldScript>(p => p.OnOpenStateChange(open));
        }

        public void OnConfigLoad(bool reload)
        {
            ForEach<WorldScript>(p => p.OnConfigLoad(reload));
        }

        public void OnMotdChange(string newMotd)
        {
            ForEach<WorldScript>(p => p.OnMotdChange(newMotd));
        }

        public void OnShutdownInitiate(ShutdownExitCode code, ShutdownMask mask)
        {
            ForEach<WorldScript>(p => p.OnShutdownInitiate(code, mask));
        }

        public void OnShutdownCancel()
        {
            ForEach<WorldScript>(p => p.OnShutdownCancel());
        }

        public void OnWorldUpdate(TimeSpan diff)
        {
            ForEach<WorldScript>(p => p.OnUpdate(diff));
        }

        //FormulaScript
        public void OnHonorCalculation(float honor, int level, float multiplier)
        {
            ForEach<FormulaScript>(p => p.OnHonorCalculation(honor, level, multiplier));
        }

        public void OnGrayLevelCalculation(int grayLevel, int playerLevel)
        {
            ForEach<FormulaScript>(p => p.OnGrayLevelCalculation(grayLevel, playerLevel));
        }

        public void OnColorCodeCalculation(XPColorChar color, int playerLevel, int mobLevel)
        {
            ForEach<FormulaScript>(p => p.OnColorCodeCalculation(color, playerLevel, mobLevel));
        }

        public void OnZeroDifferenceCalculation(int diff, int playerLevel)
        {
            ForEach<FormulaScript>(p => p.OnZeroDifferenceCalculation(diff, playerLevel));
        }

        public void OnBaseGainCalculation(int gain, int playerLevel, int mobLevel, ContentLevels content)
        {
            ForEach<FormulaScript>(p => p.OnBaseGainCalculation(gain, playerLevel, mobLevel, content));
        }

        public void OnGainCalculation(int gain, Player player, Unit unit)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(unit != null);

            ForEach<FormulaScript>(p => p.OnGainCalculation(gain, player, unit));
        }

        public void OnGroupRateCalculation(float rate, int count, bool isRaid)
        {
            ForEach<FormulaScript>(p => p.OnGroupRateCalculation(rate, count, isRaid));
        }

        //MapScript
        public void OnCreateMap(Map map)
        {
            Cypher.Assert(map != null);
            var record = map.GetEntry();

            if (record != null && record.IsWorldMap)
                ForEach<WorldMapScript>(p => p.OnCreate(map));

            if (record != null && record.IsDungeon)
                ForEach<InstanceMapScript>(p => p.OnCreate(map.ToInstanceMap()));

            if (record != null && record.IsBattleground)
                ForEach<BattlegroundMapScript>(p => p.OnCreate(map.ToBattlegroundMap()));
        }

        public void OnDestroyMap(Map map)
        {
            Cypher.Assert(map != null);
            var record = map.GetEntry();

            if (record != null && record.IsWorldMap)
                ForEach<WorldMapScript>(p => p.OnDestroy(map));

            if (record != null && record.IsDungeon)
                ForEach<InstanceMapScript>(p => p.OnDestroy(map.ToInstanceMap()));

            if (record != null && record.IsBattleground)
                ForEach<BattlegroundMapScript>(p => p.OnDestroy(map.ToBattlegroundMap()));
        }

        public void OnPlayerEnterMap(Map map, Player player)
        {
            Cypher.Assert(map != null);
            Cypher.Assert(player != null);

            ForEach<PlayerScript>(p => p.OnMapChanged(player));

            var record = map.GetEntry();

            if (record != null && record.IsWorldMap)
                ForEach<WorldMapScript>(p => p.OnPlayerEnter(map, player));

            if (record != null && record.IsDungeon)
                ForEach<InstanceMapScript>(p => p.OnPlayerEnter(map.ToInstanceMap(), player));

            if (record != null && record.IsBattleground)
                ForEach<BattlegroundMapScript>(p => p.OnPlayerEnter(map.ToBattlegroundMap(), player));
        }

        public void OnPlayerLeaveMap(Map map, Player player)
        {
            Cypher.Assert(map != null);
            var record = map.GetEntry();

            if (record != null && record.IsWorldMap)
                ForEach<WorldMapScript>(p => p.OnPlayerLeave(map, player));

            if (record != null && record.IsDungeon)
                ForEach<InstanceMapScript>(p => p.OnPlayerLeave(map.ToInstanceMap(), player));

            if (record != null && record.IsBattleground)
                ForEach<BattlegroundMapScript>(p => p.OnPlayerLeave(map.ToBattlegroundMap(), player));
        }

        public void OnMapUpdate(Map map, TimeSpan diff)
        {
            Cypher.Assert(map != null);
            var record = map.GetEntry();

            if (record != null && record.IsWorldMap)
                ForEach<WorldMapScript>(p => p.OnUpdate(map, diff));

            if (record != null && record.IsDungeon)
                ForEach<InstanceMapScript>(p => p.OnUpdate(map.ToInstanceMap(), diff));

            if (record != null && record.IsBattleground)
                ForEach<BattlegroundMapScript>(p => p.OnUpdate(map.ToBattlegroundMap(), diff));
        }

        //InstanceMapScript
        public InstanceScript CreateInstanceData(InstanceMap map)
        {
            Cypher.Assert(map != null);

            return RunScriptRet<InstanceMapScript, InstanceScript>(p => p.GetInstanceScript(map), map.GetScriptId(), null);
        }

        //ItemScript
        public bool OnQuestAccept(Player player, Item item, Quest quest)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(item != null);
            Cypher.Assert(quest != null);

            return RunScriptRet<ItemScript>(p => p.OnQuestAccept(player, item, quest), item.GetScriptId());
        }

        public bool OnItemUse(Player player, Item item, SpellCastTargets targets, ObjectGuid castId)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(item != null);

            return RunScriptRet<ItemScript>(p => p.OnUse(player, item, targets, castId), item.GetScriptId());
        }

        public bool OnItemExpire(Player player, ItemTemplate proto)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(proto != null);

            return RunScriptRet<ItemScript>(p => p.OnExpire(player, proto), proto.ScriptId);
        }

        public bool OnItemRemove(Player player, Item item)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(item != null);

            return RunScriptRet<ItemScript>(tmpscript => tmpscript.OnRemove(player, item), item.GetScriptId());
        }

        public bool OnCastItemCombatSpell(Player player, Unit victim, SpellInfo spellInfo, Item item)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(victim != null);
            Cypher.Assert(spellInfo != null);
            Cypher.Assert(item != null);

            return RunScriptRet<ItemScript>(tmpscript => 
            tmpscript.OnCastItemCombatSpell(player, victim, spellInfo, item), item.GetScriptId()
            );
        }

        public CreatureAI GetCreatureAI(Creature creature)
        {
            Cypher.Assert(creature != null);

            return RunScriptRet<CreatureScript, CreatureAI>(p => p.GetAI(creature), creature.GetScriptId());
        }

        //GameObjectScript
        public GameObjectAI GetGameObjectAI(GameObject go)
        {
            Cypher.Assert(go != null);

            return RunScriptRet<GameObjectScript, GameObjectAI>(p => p.GetAI(go), go.GetScriptId());
        }

        //AreaTriggerScript
        public bool OnAreaTrigger(Player player, AreaTriggerRecord trigger, bool entered)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(trigger != null);

            return RunScriptRet<AreaTriggerScript>(p => 
            entered ? p.OnTrigger(player, trigger) : p.OnExit(player, trigger), 
            Global.ObjectMgr.GetAreaTriggerScriptId(trigger.Id));
        }

        //BattlefieldScript
        public BattleField CreateBattlefield(int scriptId, Map map)
        {
            return RunScriptRet<BattlefieldScript, BattleField>(p => p.GetBattlefield(map), scriptId, null);
        }

        //BattlegroundScript
        public Battleground CreateBattleground(BattlegroundTypeId typeId)
        {
            // @todo Implement script-side Battlegrounds.
            Cypher.Assert(false);
            return null;
        }

        // OutdoorPvPScript
        public OutdoorPvP CreateOutdoorPvP(int scriptId, Map map)
        {
            return RunScriptRet<OutdoorPvPScript, OutdoorPvP>(p => p.GetOutdoorPvP(map), scriptId, null);
        }

        // WeatherScript
        public void OnWeatherChange(Weather weather, WeatherState state, float grade)
        {
            Cypher.Assert(weather != null);
            RunScript<WeatherScript>(p => p.OnChange(weather, state, grade), weather.GetScriptId());
        }

        public void OnWeatherUpdate(Weather weather, TimeSpan diff)
        {
            Cypher.Assert(weather != null);
            RunScript<WeatherScript>(p => p.OnUpdate(weather, diff), weather.GetScriptId());
        }

        // AuctionHouseScript
        public void OnAuctionAdd(AuctionHouseObject ah, AuctionPosting auction)
        {
            Cypher.Assert(ah != null);
            Cypher.Assert(auction != null);
            ForEach<AuctionHouseScript>(p => p.OnAuctionAdd(ah, auction));
        }

        public void OnAuctionRemove(AuctionHouseObject ah, AuctionPosting auction)
        {
            Cypher.Assert(ah != null);
            Cypher.Assert(auction != null);
            ForEach<AuctionHouseScript>(p => p.OnAuctionRemove(ah, auction));
        }

        public void OnAuctionSuccessful(AuctionHouseObject ah, AuctionPosting auction)
        {
            Cypher.Assert(ah != null);
            Cypher.Assert(auction != null);
            ForEach<AuctionHouseScript>(p => p.OnAuctionSuccessful(ah, auction));
        }

        public void OnAuctionExpire(AuctionHouseObject ah, AuctionPosting auction)
        {
            Cypher.Assert(ah != null);
            Cypher.Assert(auction != null);
            ForEach<AuctionHouseScript>(p => p.OnAuctionExpire(ah, auction));
        }

        // ConditionScript
        public bool OnConditionCheck(Condition condition, ConditionSourceInfo sourceInfo)
        {
            Cypher.Assert(condition != null);

            return RunScriptRet<ConditionScript>(p => 
            p.OnConditionCheck(condition, sourceInfo), condition.ScriptId, true);
        }

        // VehicleScript
        public void OnInstall(Vehicle veh)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));

            RunScript<VehicleScript>(p => p.OnInstall(veh), veh.GetBase().ToCreature().GetScriptId());
        }

        public void OnUninstall(Vehicle veh)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));

            RunScript<VehicleScript>(p => p.OnUninstall(veh), veh.GetBase().ToCreature().GetScriptId());
        }

        public void OnReset(Vehicle veh)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));

            RunScript<VehicleScript>(p => p.OnReset(veh), veh.GetBase().ToCreature().GetScriptId());
        }

        public void OnInstallAccessory(Vehicle veh, Creature accessory)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));
            Cypher.Assert(accessory != null);

            RunScript<VehicleScript>(p => p.OnInstallAccessory(veh, accessory), veh.GetBase().ToCreature().GetScriptId());
        }

        public void OnAddPassenger(Vehicle veh, Unit passenger, sbyte seatId)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));
            Cypher.Assert(passenger != null);

            RunScript<VehicleScript>(p => p.OnAddPassenger(veh, passenger, seatId), veh.GetBase().ToCreature().GetScriptId());
        }

        public void OnRemovePassenger(Vehicle veh, Unit passenger)
        {
            Cypher.Assert(veh != null);
            Cypher.Assert(veh.GetBase().IsTypeId(TypeId.Unit));
            Cypher.Assert(passenger != null);

            RunScript<VehicleScript>(p => p.OnRemovePassenger(veh, passenger), veh.GetBase().ToCreature().GetScriptId());
        }

        // DynamicObjectScript
        public void OnDynamicObjectUpdate(DynamicObject dynobj, TimeSpan diff)
        {
            Cypher.Assert(dynobj != null);

            ForEach<DynamicObjectScript>(p => p.OnUpdate(dynobj, diff));
        }

        // TransportScript
        public void OnAddPassenger(Transport transport, Player player)
        {
            Cypher.Assert(transport != null);
            Cypher.Assert(player != null);

            RunScript<TransportScript>(p => p.OnAddPassenger(transport, player), transport.GetScriptId());
        }

        public void OnAddCreaturePassenger(Transport transport, Creature creature)
        {
            Cypher.Assert(transport != null);
            Cypher.Assert(creature != null);

            RunScript<TransportScript>(p => p.OnAddCreaturePassenger(transport, creature), transport.GetScriptId());
        }

        public void OnRemovePassenger(Transport transport, Player player)
        {
            Cypher.Assert(transport != null);
            Cypher.Assert(player != null);

            RunScript<TransportScript>(p => p.OnRemovePassenger(transport, player), transport.GetScriptId());
        }
        
        public void OnTransportUpdate(Transport transport, TimeSpan diff)
        {
            Cypher.Assert(transport != null);

            RunScript<TransportScript>(p => p.OnUpdate(transport, diff), transport.GetScriptId());
        }

        public void OnRelocate(Transport transport, int mapId, float x, float y, float z)
        {
            RunScript<TransportScript>(p => p.OnRelocate(transport, mapId, x, y, z), transport.GetScriptId());
        }

        // Achievement
        public void OnAchievementCompleted(Player player, AchievementRecord achievement)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(achievement != null);

            RunScript<AchievementScript>(p => 
            p.OnCompleted(player, achievement), 
            Global.AchievementMgr.GetAchievementScriptId(achievement.Id));
        }

        // AchievementCriteriaScript
        public bool OnCriteriaCheck(int ScriptId, Player source, Unit target)
        {
            Cypher.Assert(source != null);
            // target can be NULL.

            return RunScriptRet<AchievementCriteriaScript>(p => p.OnCheck(source, target), ScriptId);
        }

        // PlayerScript
        public void OnPVPKill(Player killer, Player killed)
        {
            ForEach<PlayerScript>(p => p.OnPVPKill(killer, killed));
        }

        public void OnCreatureKill(Player killer, Creature killed)
        {
            ForEach<PlayerScript>(p => p.OnCreatureKill(killer, killed));
        }

        public void OnPlayerKilledByCreature(Creature killer, Player killed)
        {
            ForEach<PlayerScript>(p => p.OnPlayerKilledByCreature(killer, killed));
        }

        public void OnPlayerLevelChanged(Player player, byte oldLevel)
        {
            ForEach<PlayerScript>(p => p.OnLevelChanged(player, oldLevel));
        }

        public void OnPlayerFreeTalentPointsChanged(Player player, int newPoints)
        {
            ForEach<PlayerScript>(p => p.OnFreeTalentPointsChanged(player, newPoints));
        }

        public void OnPlayerTalentsReset(Player player, bool noCost)
        {
            ForEach<PlayerScript>(p => p.OnTalentsReset(player, noCost));
        }

        public void OnPlayerMoneyChanged(Player player, long amount)
        {
            ForEach<PlayerScript>(p => p.OnMoneyChanged(player, amount));
        }

        public void OnGivePlayerXP(Player player, ref int amount, Unit victim)
        {
            int tempAmount = amount;
            ForEach<PlayerScript>(p => tempAmount = p.OnGiveXP(player, tempAmount, victim));
            amount = tempAmount;
        }

        public void OnPlayerReputationChange(Player player, int factionID, int standing, bool incremental)
        {
            ForEach<PlayerScript>(p => p.OnReputationChange(player, factionID, standing, incremental));
        }

        public void OnPlayerDuelRequest(Player target, Player challenger)
        {
            ForEach<PlayerScript>(p => p.OnDuelRequest(target, challenger));
        }

        public void OnPlayerDuelStart(Player player1, Player player2)
        {
            ForEach<PlayerScript>(p => p.OnDuelStart(player1, player2));
        }

        public void OnPlayerDuelEnd(Player winner, Player loser, DuelCompleteType type)
        {
            ForEach<PlayerScript>(p => p.OnDuelEnd(winner, loser, type));
        }

        public void OnPlayerChat(Player player, ChatMsg type, Language lang, string msg)
        {
            ForEach<PlayerScript>(p => p.OnChat(player, type, lang, msg));
        }

        public void OnPlayerChat(Player player, ChatMsg type, Language lang, string msg, Player receiver)
        {
            ForEach<PlayerScript>(p => p.OnChat(player, type, lang, msg, receiver));
        }

        public void OnPlayerChat(Player player, ChatMsg type, Language lang, string msg, Group group)
        {
            ForEach<PlayerScript>(p => p.OnChat(player, type, lang, msg, group));
        }

        public void OnPlayerChat(Player player, ChatMsg type, Language lang, string msg, Guild guild)
        {
            ForEach<PlayerScript>(p => p.OnChat(player, type, lang, msg, guild));
        }

        public void OnPlayerChat(Player player, ChatMsg type, Language lang, string msg, Channel channel)
        {
            ForEach<PlayerScript>(p => p.OnChat(player, type, lang, msg, channel));
        }

        public void OnPlayerClearEmote(Player player)
        {
            ForEach<PlayerScript>(p => p.OnClearEmote(player));
        }

        public void OnPlayerTextEmote(Player player, int textEmote, int emoteNum, ObjectGuid guid)
        {
            ForEach<PlayerScript>(p => p.OnTextEmote(player, textEmote, emoteNum, guid));
        }

        public void OnPlayerSpellCast(Player player, Spell spell, bool skipCheck)
        {
            ForEach<PlayerScript>(p => p.OnSpellCast(player, spell, skipCheck));
        }

        public void OnPlayerLogin(Player player, bool firstLogin)
        {
            ForEach<PlayerScript>(p => p.OnLogin(player, firstLogin));
        }

        public void OnPlayerLogout(Player player)
        {
            ForEach<PlayerScript>(p => p.OnLogout(player));
        }

        public void OnPlayerCreate(Player player)
        {
            ForEach<PlayerScript>(p => p.OnCreate(player));
        }

        public void OnPlayerDelete(ObjectGuid guid, int accountId)
        {
            ForEach<PlayerScript>(p => p.OnDelete(guid, accountId));
        }

        public void OnPlayerFailedDelete(ObjectGuid guid, int accountId)
        {
            ForEach<PlayerScript>(p => p.OnFailedDelete(guid, accountId));
        }

        public void OnPlayerSave(Player player)
        {
            ForEach<PlayerScript>(p => p.OnSave(player));
        }

        public void OnPlayerBindToInstance(Player player, Difficulty difficulty, int mapid, bool permanent, byte extendState)
        {
            ForEach<PlayerScript>(p => p.OnBindToInstance(player, difficulty, mapid, permanent, extendState));
        }

        public void OnPlayerUpdateZone(Player player, int newZone, int newArea)
        {
            ForEach<PlayerScript>(p => p.OnUpdateZone(player, newZone, newArea));
        }

        public void OnPlayerRepop(Player player)
        {
            ForEach<PlayerScript>(p => p.OnPlayerRepop(player));
        }

        public void OnQuestStatusChange(Player player, int questId)
        {
            ForEach<PlayerScript>(p => p.OnQuestStatusChange(player, questId));
        }

        public void OnMovieComplete(Player player, int movieId)
        {
            ForEach<PlayerScript>(p => p.OnMovieComplete(player, movieId));
        }

        public void OnPlayerChoiceResponse(Player player, int choiceId, int responseId)
        {
            ForEach<PlayerScript>(p => p.OnPlayerChoiceResponse(player, choiceId, responseId));
        }

        // Account
        public void OnAccountLogin(int accountId)
        {
            ForEach<AccountScript>(script => script.OnAccountLogin(accountId));
        }

        public void OnFailedAccountLogin(int accountId)
        {
            ForEach<AccountScript>(script => script.OnFailedAccountLogin(accountId));
        }

        public void OnEmailChange(int accountId)
        {
            ForEach<AccountScript>(script => script.OnEmailChange(accountId));
        }

        public void OnFailedEmailChange(int accountId)
        {
            ForEach<AccountScript>(script => script.OnFailedEmailChange(accountId));
        }

        public void OnPasswordChange(int accountId)
        {
            ForEach<AccountScript>(script => script.OnPasswordChange(accountId));
        }

        public void OnFailedPasswordChange(int accountId)
        {
            ForEach<AccountScript>(script => script.OnFailedPasswordChange(accountId));
        }

        // GuildScript
        public void OnGuildAddMember(Guild guild, Player player, byte plRank)
        {
            ForEach<GuildScript>(p => p.OnAddMember(guild, player, plRank));
        }

        public void OnGuildRemoveMember(Guild guild, Player player, bool isDisbanding, bool isKicked)
        {
            ForEach<GuildScript>(p => p.OnRemoveMember(guild, player, isDisbanding, isKicked));
        }

        public void OnGuildMOTDChanged(Guild guild, string newMotd)
        {
            ForEach<GuildScript>(p => p.OnMOTDChanged(guild, newMotd));
        }

        public void OnGuildInfoChanged(Guild guild, string newInfo)
        {
            ForEach<GuildScript>(p => p.OnInfoChanged(guild, newInfo));
        }

        public void OnGuildCreate(Guild guild, Player leader, string name)
        {
            ForEach<GuildScript>(p => p.OnCreate(guild, leader, name));
        }

        public void OnGuildDisband(Guild guild)
        {
            ForEach<GuildScript>(p => p.OnDisband(guild));
        }

        public void OnGuildMemberWitdrawMoney(Guild guild, Player player, long amount, bool isRepair)
        {
            ForEach<GuildScript>(p => p.OnMemberWitdrawMoney(guild, player, amount, isRepair));
        }

        public void OnGuildMemberDepositMoney(Guild guild, Player player, long amount)
        {
            ForEach<GuildScript>(p => p.OnMemberDepositMoney(guild, player, amount));
        }

        public void OnGuildItemMove(Guild guild, Player player, Item pItem, bool isSrcBank, ItemPos src, bool isDestBank, ItemPos dest)
        {
            ForEach<GuildScript>(p => p.OnItemMove(guild, player, pItem, isSrcBank, src, isDestBank, dest));
        }

        public void OnGuildEvent(Guild guild, byte eventType, long playerGuid1, long playerGuid2, byte newRank)
        {
            ForEach<GuildScript>(p => p.OnEvent(guild, eventType, playerGuid1, playerGuid2, newRank));
        }

        public void OnGuildBankEvent(Guild guild, byte eventType, byte tabId, long playerGuid, int itemOrMoney, ushort itemStackCount, byte destTabId)
        {
            ForEach<GuildScript>(p => 
            p.OnBankEvent(guild, eventType, tabId, playerGuid, itemOrMoney, itemStackCount, destTabId));
        }

        // GroupScript
        public void OnGroupAddMember(Group group, ObjectGuid guid)
        {
            Cypher.Assert(group != null);
            ForEach<GroupScript>(p => p.OnAddMember(group, guid));
        }

        public void OnGroupInviteMember(Group group, ObjectGuid guid)
        {
            Cypher.Assert(group != null);
            ForEach<GroupScript>(p => p.OnInviteMember(group, guid));
        }

        public void OnGroupRemoveMember(Group group, ObjectGuid guid, RemoveMethod method, ObjectGuid kicker, string reason)
        {
            Cypher.Assert(group != null);
            ForEach<GroupScript>(p => p.OnRemoveMember(group, guid, method, kicker, reason));
        }

        public void OnGroupChangeLeader(Group group, ObjectGuid newLeaderGuid, ObjectGuid oldLeaderGuid)
        {
            Cypher.Assert(group != null);
            ForEach<GroupScript>(p => p.OnChangeLeader(group, newLeaderGuid, oldLeaderGuid));
        }

        public void OnGroupDisband(Group group)
        {
            Cypher.Assert(group != null);
            ForEach<GroupScript>(p => p.OnDisband(group));
        }

        // UnitScript
        public void OnHeal(Unit healer, Unit reciever, ref int gain)
        {
            var dmg = gain;
            ForEach<UnitScript>(p => p.OnHeal(healer, reciever, ref dmg));
            gain = dmg;
        }

        public void OnDamage(Unit attacker, Unit victim, ref int damage)
        {
            var dmg = damage;
            ForEach<UnitScript>(p => p.OnDamage(attacker, victim, ref dmg));
            damage = dmg;
        }

        public void ModifyPeriodicDamageAurasTick(Unit target, Unit attacker, ref int damage)
        {
            var dmg = damage;
            ForEach<UnitScript>(p => p.ModifyPeriodicDamageAurasTick(target, attacker, ref dmg));
            damage = dmg;
        }

        public void ModifyMeleeDamage(Unit target, Unit attacker, ref int damage)
        {
            var dmg = damage;
            ForEach<UnitScript>(p => p.ModifyMeleeDamage(target, attacker, ref dmg));
            damage = dmg;
        }

        public void ModifySpellDamageTaken(Unit target, Unit attacker, ref int damage, SpellInfo spellInfo)
        {
            var dmg = damage;
            ForEach<UnitScript>(p => p.ModifySpellDamageTaken(target, attacker, ref dmg, spellInfo));
            damage = dmg;
        }

        // AreaTriggerEntityScript
        public AreaTriggerAI GetAreaTriggerAI(AreaTrigger areaTrigger)
        {
            Cypher.Assert(areaTrigger != null);

            return RunScriptRet<AreaTriggerEntityScript, AreaTriggerAI>(p => 
            p.GetAI(areaTrigger), areaTrigger.GetScriptId(), null);
        }

        // ConversationScript
        public void OnConversationCreate(Conversation conversation, Unit creator)
        {
            Cypher.Assert(conversation != null);

            RunScript<ConversationScript>(script => 
            script.OnConversationCreate(conversation, creator), conversation.GetScriptId());
        }

        public void OnConversationStart(Conversation conversation)
        {
            Cypher.Assert(conversation != null);

            RunScript<ConversationScript>(script => script.OnConversationStart(conversation), conversation.GetScriptId());
        }

        public void OnConversationLineStarted(Conversation conversation, uint lineId, Player sender)
        {
            Cypher.Assert(conversation != null);
            Cypher.Assert(sender != null);

            RunScript<ConversationScript>(script => script.OnConversationLineStarted(conversation, lineId, sender), conversation.GetScriptId());
        }

        public void OnConversationUpdate(Conversation conversation, TimeSpan diff)
        {
            Cypher.Assert(conversation != null);

            RunScript<ConversationScript>(script => 
            script.OnConversationUpdate(conversation, diff), conversation.GetScriptId());
        }

        //SceneScript
        public void OnSceneStart(Player player, int sceneInstanceID, SceneTemplate sceneTemplate)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(sceneTemplate != null);

            RunScript<SceneScript>(script => 
            script.OnSceneStart(player, sceneInstanceID, sceneTemplate), sceneTemplate.ScriptId);
        }

        public void OnSceneTrigger(Player player, int sceneInstanceID, SceneTemplate sceneTemplate, string triggerName)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(sceneTemplate != null);

            RunScript<SceneScript>(script => 
            script.OnSceneTriggerEvent(player, sceneInstanceID, sceneTemplate, triggerName), 
            sceneTemplate.ScriptId);
        }

        public void OnSceneCancel(Player player, int sceneInstanceID, SceneTemplate sceneTemplate)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(sceneTemplate != null);

            RunScript<SceneScript>(script => 
            script.OnSceneCancel(player, sceneInstanceID, sceneTemplate), 
            sceneTemplate.ScriptId);
        }

        public void OnSceneComplete(Player player, int sceneInstanceID, SceneTemplate sceneTemplate)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(sceneTemplate != null);

            RunScript<SceneScript>(script => 
            script.OnSceneComplete(player, sceneInstanceID, sceneTemplate), 
            sceneTemplate.ScriptId);
        }

        //QuestScript
        public void OnQuestStatusChange(Player player, Quest quest, QuestStatus oldStatus, QuestStatus newStatus)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(quest != null);

            RunScript<QuestScript>(script => script.OnQuestStatusChange(player, quest, oldStatus, newStatus), quest.ScriptId);
        }

        public void OnQuestAcknowledgeAutoAccept(Player player, Quest quest)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(quest != null);

            RunScript<QuestScript>(script => script.OnAcknowledgeAutoAccept(player, quest), quest.ScriptId);
        }

        public void OnQuestObjectiveChange(Player player, Quest quest, QuestObjective objective, int oldAmount, int newAmount)
        {
            Cypher.Assert(player != null);
            Cypher.Assert(quest != null);

            RunScript<QuestScript>(script => 
            script.OnQuestObjectiveChange(player, quest, objective, oldAmount, newAmount), 
            quest.ScriptId);
        }

        // WorldState
        public void OnWorldStateValueChange(WorldStateTemplate worldStateTemplate, WorldStateValue oldValue, WorldStateValue newValue, Map map)
        {
            Cypher.Assert(worldStateTemplate != null);

            RunScript<WorldStateScript>(script => 
            script.OnValueChange(worldStateTemplate.Id, oldValue, newValue, map), 
            worldStateTemplate.ScriptId);
        }

        // EventScript
        public void OnEventTrigger(WorldObject obj, WorldObject invoker, int eventId)
        {
            Cypher.Assert(invoker != null);

            RunScript<EventScript>(script => 
            script.OnTrigger(obj, invoker, eventId), 
            Global.ObjectMgr.GetEventScriptId(eventId));
        }

        public void ForEach<T>(Action<T> a) where T : ScriptObject
        {
            var reg = GetScriptRegistry<T>();
            if (reg == null || reg.Empty())
                return;

            foreach (var script in reg.GetStorage())
                a.Invoke(script);
        }

        public bool RunScriptRet<T>(Func<T, bool> func, int id, bool ret = false) where T : ScriptObject
        {
            return RunScriptRet<T, bool>(func, id, ret);
        }

        public U RunScriptRet<T, U>(Func<T, U> func, int id, U ret = default) where T : ScriptObject
        {
            var reg = GetScriptRegistry<T>();
            if (reg == null || reg.Empty())
                return ret;

            var script = reg.GetScriptById(id);
            if (script == null)
                return ret;

            return func.Invoke(script);
        }

        public void RunScript<T>(Action<T> a, int id) where T : ScriptObject
        {
            var reg = GetScriptRegistry<T>();
            if (reg == null || reg.Empty())
                return;

            var script = reg.GetScriptById(id);
            if (script != null)
                a.Invoke(script);
        }

        public void AddScript<T>(T script) where T : ScriptObject
        {
            Cypher.Assert(script != null);

            if (!ScriptStorage.ContainsKey(typeof(T)))
                ScriptStorage[typeof(T)] = new ScriptRegistry<T>();

            GetScriptRegistry<T>().AddScript(script);
        }

        ScriptRegistry<T> GetScriptRegistry<T>() where T : ScriptObject
        {
            if (ScriptStorage.ContainsKey(typeof(T)))
                return (ScriptRegistry<T>)ScriptStorage[typeof(T)];

            return null;
        }

        uint _ScriptCount;
        public Dictionary<int, SpellSummary> spellSummaryStorage = new();
        Hashtable ScriptStorage = new();

        // creature entry + chain ID
        MultiMap<(int, ushort), SplineChainLink> m_mSplineChainsMap = new(); // spline chains
    }

    public interface IScriptRegistry
    {
        void Unload();
    }

    public class ScriptRegistry<TValue> : IScriptRegistry where TValue : ScriptObject
    {
        public void AddScript(TValue script)
        {
            Cypher.Assert(script != null);

            if (!script.IsDatabaseBound())
            {
                // We're dealing with a code-only script; just add it.
                ScriptMap[_scriptIdCounter++] = script;
                Global.ScriptMgr.IncrementScriptCount();
                return;
            }

            // Get an ID for the script. An ID only exists if it's a script that is assigned in the database
            // through a script name (or similar).
            int id = Global.ObjectMgr.GetScriptId(script.GetName());
            if (id != 0)
            {
                // Try to find an existing script.
                bool existing = false;
                foreach (var it in ScriptMap)
                {
                    if (it.Value.GetName() == script.GetName())
                    {
                        existing = true;
                        break;
                    }
                }

                // If the script isn't assigned . assign it!
                if (!existing)
                {
                    ScriptMap[id] = script;
                    Global.ScriptMgr.IncrementScriptCount();
                }
                else
                {
                    // If the script is already assigned . delete it!
                    Log.outError(LogFilter.Scripts, 
                        $"Script '{script.GetName()}' already assigned with the same script name, " +
                        $"so the script can't work.");

                    Cypher.Assert(false); // Error that should be fixed ASAP.
                }
            }
            else
            {
                // The script uses a script name from database, but isn't assigned to anything.
                Log.outError(LogFilter.Sql, 
                    $"Script named '{script.GetName()}' does not have " +
                    $"a script name assigned in database.");
                return;
            }
        }

        // Gets a script by its ID (assigned by ObjectMgr).
        public TValue GetScriptById(int id)
        {
            return ScriptMap.LookupByKey(id);
        }

        public bool Empty()
        {
            return ScriptMap.Empty();
        }

        public ICollection<TValue> GetStorage()
        {
            return ScriptMap.Values;
        }

        public void Unload()
        {
            ScriptMap.Clear();
        }

        // Counter used for code-only scripts.
        int _scriptIdCounter;
        Dictionary<int, TValue> ScriptMap = new();
    }

    public class SpellSummary
    {
        public byte Targets;                                          // set of enum SelectTarget
        public byte Effects;                                          // set of enum SelectEffect
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ScriptAttribute : Attribute
    {
        public ScriptAttribute(string name = "", params object[] args)
        {
            Name = name;
            Args = args;
        }

        public string Name { get; private set; }
        public object[] Args { get; private set; }
    }
}
