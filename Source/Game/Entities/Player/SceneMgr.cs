﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.DataStorage;
using Game.Networking;
using Game.Networking.Packets;
using System.Collections.Generic;

namespace Game.Entities
{
    public class SceneMgr
    {
        Player _player;
        Dictionary<int, SceneTemplate> _scenesByInstance = new();
        int _standaloneSceneInstanceID;
        List<ServerPacket> _delayedScenes = new();
        bool _isDebuggingScenes;

        public SceneMgr(Player player)
        {
            _player = player;
            _standaloneSceneInstanceID = 0;
            _isDebuggingScenes = false;
        }

        public int PlayScene(int sceneId, Position position = null)
        {
            SceneTemplate sceneTemplate = Global.ObjectMgr.GetSceneTemplate(sceneId);
            return PlaySceneByTemplate(sceneTemplate, position);
        }

        public int PlaySceneByTemplate(SceneTemplate sceneTemplate, Position position = null)
        {
            if (sceneTemplate == null)
                return 0;

            SceneScriptPackageRecord entry = CliDB.SceneScriptPackageStorage.LookupByKey(sceneTemplate.ScenePackageId);
            if (entry == null)
                return 0;

            // By default, take player position
            if (position == null)
                position = GetPlayer();

            int sceneInstanceID = GetNewStandaloneSceneInstanceID();

            if (_isDebuggingScenes)
                GetPlayer().SendSysMessage(CypherStrings.CommandSceneDebugPlay, sceneInstanceID, sceneTemplate.ScenePackageId, sceneTemplate.PlaybackFlags);

            PlayScene playScene = new();
            playScene.SceneID = sceneTemplate.SceneId;
            playScene.PlaybackFlags = (uint)sceneTemplate.PlaybackFlags;
            playScene.SceneInstanceID = sceneInstanceID;
            playScene.SceneScriptPackageID = sceneTemplate.ScenePackageId;
            playScene.Location = position;
            if (GetPlayer().GetVehicle() == null) // skip vehicles passed as transport here until further research
                playScene.TransportGUID = GetPlayer().GetTransGUID();

            playScene.Encrypted = sceneTemplate.Encrypted;
            playScene.Write();

            if (GetPlayer().IsInWorld)
                GetPlayer().SendPacket(playScene);
            else
                _delayedScenes.Add(playScene);

            AddInstanceIdToSceneMap(sceneInstanceID, sceneTemplate);

            Global.ScriptMgr.OnSceneStart(GetPlayer(), sceneInstanceID, sceneTemplate);

            return sceneInstanceID;
        }

        public int PlaySceneByPackageId(int sceneScriptPackageId, SceneFlags playbackflags, Position position = null)
        {
            SceneTemplate sceneTemplate = new();
            sceneTemplate.SceneId = 0;
            sceneTemplate.ScenePackageId = sceneScriptPackageId;
            sceneTemplate.PlaybackFlags = playbackflags;
            sceneTemplate.Encrypted = false;
            sceneTemplate.ScriptId = 0;

            return PlaySceneByTemplate(sceneTemplate, position);
        }

        void CancelScene(int sceneInstanceID, bool removeFromMap = true)
        {
            if (removeFromMap)
                RemoveSceneInstanceId(sceneInstanceID);

            CancelScene cancelScene = new();
            cancelScene.SceneInstanceID = sceneInstanceID;
            GetPlayer().SendPacket(cancelScene);
        }

        public void OnSceneTrigger(int sceneInstanceID, string triggerName)
        {
            if (!HasScene(sceneInstanceID))
                return;

            if (_isDebuggingScenes)
                GetPlayer().SendSysMessage(CypherStrings.CommandSceneDebugTrigger, sceneInstanceID, triggerName);

            SceneTemplate sceneTemplate = GetSceneTemplateFromInstanceId(sceneInstanceID);
            Global.ScriptMgr.OnSceneTrigger(GetPlayer(), sceneInstanceID, sceneTemplate, triggerName);
        }

        public void OnSceneCancel(int sceneInstanceID)
        {
            if (!HasScene(sceneInstanceID))
                return;

            if (_isDebuggingScenes)
                GetPlayer().SendSysMessage(CypherStrings.CommandSceneDebugCancel, sceneInstanceID);

            SceneTemplate sceneTemplate = GetSceneTemplateFromInstanceId(sceneInstanceID);
            if (sceneTemplate.PlaybackFlags.HasFlag(SceneFlags.NotCancelable))
                return;

            // Must be done before removing aura
            RemoveSceneInstanceId(sceneInstanceID);

            if (sceneTemplate.SceneId != 0)
                RemoveAurasDueToSceneId(sceneTemplate.SceneId);

            Global.ScriptMgr.OnSceneCancel(GetPlayer(), sceneInstanceID, sceneTemplate);

            if (sceneTemplate.PlaybackFlags.HasFlag(SceneFlags.FadeToBlackscreenOnCancel))
                CancelScene(sceneInstanceID, false);
        }

        public void OnSceneComplete(int sceneInstanceID)
        {
            if (!HasScene(sceneInstanceID))
                return;

            if (_isDebuggingScenes)
                GetPlayer().SendSysMessage(CypherStrings.CommandSceneDebugComplete, sceneInstanceID);

            SceneTemplate sceneTemplate = GetSceneTemplateFromInstanceId(sceneInstanceID);

            // Must be done before removing aura
            RemoveSceneInstanceId(sceneInstanceID);

            if (sceneTemplate.SceneId != 0)
                RemoveAurasDueToSceneId(sceneTemplate.SceneId);

            Global.ScriptMgr.OnSceneComplete(GetPlayer(), sceneInstanceID, sceneTemplate);

            if (sceneTemplate.PlaybackFlags.HasFlag(SceneFlags.FadeToBlackscreenOnComplete))
                CancelScene(sceneInstanceID, false);
        }

        bool HasScene(int sceneInstanceID, int sceneScriptPackageId = 0)
        {
            var sceneTempalte = _scenesByInstance.LookupByKey(sceneInstanceID);

            if (sceneTempalte != null)
                return sceneScriptPackageId == 0 || sceneScriptPackageId == sceneTempalte.ScenePackageId;

            return false;
        }

        void AddInstanceIdToSceneMap(int sceneInstanceID, SceneTemplate sceneTemplate)
        {
            _scenesByInstance[sceneInstanceID] = sceneTemplate;
        }

        public void CancelSceneBySceneId(int sceneId)
        {
            List<int> instancesIds = new();

            foreach (var pair in _scenesByInstance)
                if (pair.Value.SceneId == sceneId)
                    instancesIds.Add(pair.Key);

            foreach (var sceneInstanceID in instancesIds)
                CancelScene(sceneInstanceID);
        }

        public void CancelSceneByPackageId(int sceneScriptPackageId)
        {
            List<int> instancesIds = new();

            foreach (var sceneTemplate in _scenesByInstance)
                if (sceneTemplate.Value.ScenePackageId == sceneScriptPackageId)
                    instancesIds.Add(sceneTemplate.Key);

            foreach (var sceneInstanceID in instancesIds)
                CancelScene(sceneInstanceID);
        }

        void RemoveSceneInstanceId(int sceneInstanceID)
        {
            _scenesByInstance.Remove(sceneInstanceID);
        }

        void RemoveAurasDueToSceneId(int sceneId)
        {
            var scenePlayAuras = GetPlayer().GetAuraEffectsByTypeCopy(AuraType.PlayScene);
            foreach (var scenePlayAura in scenePlayAuras)
            {
                if (scenePlayAura.GetMiscValue() == sceneId)
                {
                    GetPlayer().RemoveAura(scenePlayAura.GetBase());
                    break;
                }
            }
        }

        SceneTemplate GetSceneTemplateFromInstanceId(int sceneInstanceID)
        {
            return _scenesByInstance.LookupByKey(sceneInstanceID);
        }

        public int GetActiveSceneCount(int sceneScriptPackageId = 0)
        {
            int activeSceneCount = 0;

            foreach (var sceneTemplate in _scenesByInstance.Values)
                if (sceneScriptPackageId == 0 || sceneTemplate.ScenePackageId == sceneScriptPackageId)
                    ++activeSceneCount;

            return activeSceneCount;
        }

        public void TriggerDelayedScenes()
        {
            foreach (var playScene in _delayedScenes)
                GetPlayer().SendPacket(playScene);

            _delayedScenes.Clear();
        }

        Player GetPlayer() { return _player; }

        void RecreateScene(int sceneScriptPackageId, SceneFlags playbackflags, Position position = null)
        {
            CancelSceneByPackageId(sceneScriptPackageId);
            PlaySceneByPackageId(sceneScriptPackageId, playbackflags, position);
        }

        public Dictionary<int, SceneTemplate> GetSceneTemplateByInstanceMap() { return _scenesByInstance; }

        int GetNewStandaloneSceneInstanceID() { return ++_standaloneSceneInstanceID; }

        public void ToggleDebugSceneMode() { _isDebuggingScenes = !_isDebuggingScenes; }
        public bool IsInDebugSceneMode() { return _isDebuggingScenes; }
    }
}
