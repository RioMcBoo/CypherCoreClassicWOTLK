﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework;
using Framework.Constants;
using Game.Networking.Packets;
using System;

namespace Game
{
    public partial class WorldSession
    {
        public void SendAuthResponse(BattlenetRpcErrorCode code, bool queued, uint queuePos = 0)
        {
            AuthResponse response = new();
            response.Result = code;

            if (code == BattlenetRpcErrorCode.Ok)
            {
                response.SuccessInfo = new();

                response.SuccessInfo = new AuthResponse.AuthSuccessInfo();
                response.SuccessInfo.ActiveExpansionLevel = (byte)GetExpansion();
                response.SuccessInfo.AccountExpansionLevel = (byte)GetAccountExpansion();
                response.SuccessInfo.VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();
                response.SuccessInfo.Time = LoopTime.UnixServerTime;

                var realm = Global.WorldMgr.GetRealm();

                // Send current home realm. Also there is no need to send it later in realm queries.
                response.SuccessInfo.VirtualRealms.Add(new VirtualRealmInfo(realm.Id.GetAddress(), true, false, realm.Name, realm.NormalizedName));

                if (HasPermission(RBACPermissions.UseCharacterTemplates))
                    foreach (var templ in Global.CharacterTemplateDataStorage.GetCharacterTemplates().Values)
                        response.SuccessInfo.Templates.Add(templ);

                response.SuccessInfo.AvailableClasses = Global.ObjectMgr.GetClassExpansionRequirements();
            }

            if (queued)
            {
                AuthWaitInfo waitInfo = new();
                waitInfo.WaitCount = queuePos;
                response.WaitInfo = waitInfo;
            }

            SendPacket(response);
        }

        public void SendAuthWaitQueue(uint position)
        {
            if (position != 0)
            {
                WaitQueueUpdate waitQueueUpdate = new();
                waitQueueUpdate.WaitInfo.WaitCount = position;
                waitQueueUpdate.WaitInfo.WaitTime = 0;
                waitQueueUpdate.WaitInfo.HasFCM = false;
                SendPacket(waitQueueUpdate);
            }
            else
                SendPacket(new WaitQueueFinish());
        }

        public void SendClientCacheVersion(uint version)
        {
            ClientCacheVersion cache = new();
            cache.CacheVersion = version;
            SendPacket(cache);
        }

        public void SendSetTimeZoneInformation()
        {
            TimeSpan timezoneOffset = Timezone.GetSystemZoneOffset();
            string realTimezone = Timezone.GetSystemZoneName();
            string clientSupportedTZ = Timezone.FindClosestClientSupportedTimezone(realTimezone, timezoneOffset);

            SetTimeZoneInformation packet = new();
            packet.ServerTimeTZ = clientSupportedTZ;
            packet.GameTimeTZ = clientSupportedTZ;
            packet.ServerRegionalTZ = clientSupportedTZ;

            SendPacket(packet);
        }

        public void SendFeatureSystemStatusGlueScreen()
        {
            FeatureSystemStatusGlueScreen features = new();
            features.BpayStoreAvailable = false;
            features.BpayStoreDisabledByParentalControls = false;
            features.CharUndeleteEnabled = WorldConfig.Values[WorldCfg.FeatureSystemCharacterUndeleteEnabled].Bool;
            features.BpayStoreEnabled = WorldConfig.Values[WorldCfg.FeatureSystemBpayStoreEnabled].Bool;
            features.MaxCharactersPerRealm = WorldConfig.Values[WorldCfg.CharactersPerRealm].Int32 ;
            features.MinimumExpansionLevel = Expansion.Classic;
            features.MaximumExpansionLevel = (Expansion)WorldConfig.Values[WorldCfg.Expansion].Int32;

            var europaTicketConfig = new EuropaTicketConfig();
            europaTicketConfig.ThrottleState.MaxTries = 10;
            europaTicketConfig.ThrottleState.PerMilliseconds = 60000;
            europaTicketConfig.ThrottleState.TryCount = 1;
            europaTicketConfig.ThrottleState.LastResetTimeBeforeNow = 111111;
            europaTicketConfig.TicketsEnabled = WorldConfig.Values[WorldCfg.SupportTicketsEnabled].Bool;
            europaTicketConfig.BugsEnabled = WorldConfig.Values[WorldCfg.SupportBugsEnabled].Bool;
            europaTicketConfig.ComplaintsEnabled = WorldConfig.Values[WorldCfg.SupportComplaintsEnabled].Bool;
            europaTicketConfig.SuggestionsEnabled = WorldConfig.Values[WorldCfg.SupportSuggestionsEnabled].Bool;

            features.EuropaTicketSystemStatus = europaTicketConfig;

            SendPacket(features);
        }
    }
}
