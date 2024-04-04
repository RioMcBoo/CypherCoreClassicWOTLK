// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public struct ObjectGuid : IEquatable<ObjectGuid>
    {
        public static ObjectGuid Empty = new();
        public static ObjectGuid FromStringFailed = Create(HighGuid.Uniq, 4);
        public static ObjectGuid TradeItem = Create(HighGuid.Uniq, 10);

        long _low;
        long _high;

        public ObjectGuid(long high, long low)
        {
            _low = low;
            _high = high;
        }

        public static ObjectGuid Create(HighGuid type, long dbId)
        {
            switch (type)
            {
                case HighGuid.Null:
                    return ObjectGuidFactory.CreateNull();
                case HighGuid.Uniq:
                    return ObjectGuidFactory.CreateUniq(dbId);
                case HighGuid.Player:
                    return ObjectGuidFactory.CreatePlayer(0, dbId);
                case HighGuid.Item:
                    return ObjectGuidFactory.CreateItem(0, dbId);
                case HighGuid.StaticDoor:
                case HighGuid.Transport:
                    return ObjectGuidFactory.CreateTransport(type, dbId);
                case HighGuid.Party:
                case HighGuid.WowAccount:
                case HighGuid.BNetAccount:
                case HighGuid.GMTask:
                case HighGuid.RaidGroup:
                case HighGuid.Spell:
                case HighGuid.Mail:
                case HighGuid.UserRouter:
                case HighGuid.PVPQueueGroup:
                case HighGuid.UserClient:
                case HighGuid.BattlePet:
                case HighGuid.CommerceObj:
                    return ObjectGuidFactory.CreateGlobal(type, 0, dbId);
                case HighGuid.Guild:
                    return ObjectGuidFactory.CreateGuild(type, 0, dbId);
                default:
                    return Empty;
            }
        }

        public static ObjectGuid Create(HighGuid type, ushort ownerType, ushort ownerId, uint counter)
        {
            if (type != HighGuid.ClientActor)
                return Empty;

            return ObjectGuidFactory.CreateClientActor(ownerType, ownerId, counter);
        }

        public static ObjectGuid Create(HighGuid type, bool builtIn, bool trade, ushort zoneId, byte factionGroupMask, long counter)
        {
            if (type != HighGuid.ChatChannel)
                return Empty;

            return ObjectGuidFactory.CreateChatChannel(0, builtIn, trade, zoneId, factionGroupMask, counter);
        }

        public static ObjectGuid Create(HighGuid type, ushort arg1, long counter)
        {
            if (type != HighGuid.MobileSession)
                return Empty;

            return ObjectGuidFactory.CreateMobileSession(0, arg1, counter);
        }

        public static ObjectGuid Create(HighGuid type, byte arg1, byte arg2, long counter)
        {
            if (type != HighGuid.WebObj)
                return Empty;

            return ObjectGuidFactory.CreateWebObj(0, arg1, arg2, counter);
        }

        public static ObjectGuid Create(HighGuid type, byte arg1, byte arg2, byte arg3, byte arg4, bool arg5, byte arg6, long counter)
        {
            if (type != HighGuid.LFGObject)
                return Empty;

            return ObjectGuidFactory.CreateLFGObject(arg1, arg2, arg3, arg4, arg5, arg6, counter);
        }

        public static ObjectGuid Create(HighGuid type, byte arg1, long counter)
        {
            if (type != HighGuid.LFGList)
                return Empty;

            return ObjectGuidFactory.CreateLFGList(arg1, counter);
        }

        public static ObjectGuid Create(HighGuid type, int arg1, long counter)
        {
            switch (type)
            {
                case HighGuid.PetBattle:
                case HighGuid.UniqUserClient:
                case HighGuid.ClientSession:
                case HighGuid.ClientConnection:
                case HighGuid.LMMParty:
                    return ObjectGuidFactory.CreateClient(type, 0, arg1, counter);
                default:
                    return Empty;
            }
        }

        public static ObjectGuid Create(HighGuid type, byte clubType, int clubFinderId, long counter)
        {
            if (type != HighGuid.ClubFinder)
                return Empty;

            return ObjectGuidFactory.CreateClubFinder(0, clubType, clubFinderId, counter);
        }

        public static ObjectGuid Create(HighGuid type, int mapId, int entry, long counter)
        {
            switch (type)
            {
                case HighGuid.WorldTransaction:
                case HighGuid.Conversation:
                case HighGuid.Creature:
                case HighGuid.Vehicle:
                case HighGuid.Pet:
                case HighGuid.GameObject:
                case HighGuid.DynamicObject:
                case HighGuid.AreaTrigger:
                case HighGuid.Corpse:
                case HighGuid.LootObject:
                case HighGuid.SceneObject:
                case HighGuid.Scenario:
                case HighGuid.AIGroup:
                case HighGuid.DynamicDoor:
                case HighGuid.Vignette:
                case HighGuid.CallForHelp:
                case HighGuid.AIResource:
                case HighGuid.AILock:
                case HighGuid.AILockTicket:
                    return ObjectGuidFactory.CreateWorldObject(type, 0, 0, (ushort)mapId, 0, entry, counter);
                case HighGuid.ToolsClient:
                    return ObjectGuidFactory.CreateToolsClient(mapId, entry, counter); 
                default:
                    return Empty;
            }
        }

        public static ObjectGuid Create(HighGuid type, SpellCastSource subType, int mapId, int entry, long counter)
        {
            switch (type)
            {
                case HighGuid.Cast:
                    return ObjectGuidFactory.CreateWorldObject(type, (byte)subType, 0, (ushort)mapId, 0, entry, counter);
                default:
                    return Empty;
            }
        }

        public static ObjectGuid Create(HighGuid type, uint arg1, ushort arg2, byte arg3, uint arg4)
        {
            if (type != HighGuid.WorldLayer)
                return Empty;
            
            return ObjectGuidFactory.CreateWorldLayer(arg1, arg2, arg3, arg4);
        }

        public static ObjectGuid Create(HighGuid type, int arg2, byte arg3, byte arg4, long counter)
        {
            if (type != HighGuid.LMMLobby)
                return Empty;

            return ObjectGuidFactory.CreateLMMLobby(0, arg2, arg3, arg4, counter);
        }

        public byte[] GetRawValue()
        {
            byte[] temp = new byte[16];
            var hiBytes = BitConverter.GetBytes(_high);
            var lowBytes = BitConverter.GetBytes(_low);
            for (var i = 0; i < temp.Length / 2; ++i)
            {
                temp[i] = lowBytes[i];
                temp[8 + i] = hiBytes[i];
            }

            return temp;
        }
        public void SetRawValue(byte[] bytes)
        {
            _low = BitConverter.ToInt64(bytes, 0);
            _high = BitConverter.ToInt64(bytes, 8);
        }

        public void SetRawValue(long high, long low) { _high = high; _low = low; }
        public void Clear() { _high = 0; _low = 0; }
        public long GetHighValue()
        {
            return _high;
        }
        public long GetLowValue()
        {
            return _low;
        }

        public HighGuid GetHigh() { return (HighGuid)(_high >> 58); }
        public int GetSubType() { return (byte)_high & 0x3F; }
        public int GetRealmId() { return (int)(_high >> 42) & 0x1FFF; }
        public int GetServerId() { return (int)(_low >> 40) & 0x1FFF; }
        public int GetMapId() { return (int)(_high >> 29) & 0x1FFF; }
        public int GetEntry() { return (int)(_high >> 6) & 0x7FFFFF; }
        public long GetCounter()
        {
            if (GetHigh() == HighGuid.Transport)
                return (_high >> 38) & 0xFFFFF;
            else
                return _low & 0xFFFFFFFFFF;
        }
        public static long GetMaxCounter(HighGuid highGuid)
        {
            if (highGuid == HighGuid.Transport)
                return 0xFFFFF;
            else
                return 0xFFFFFFFFFF;
        }

        public bool IsEmpty() { return _low == 0 && _high == 0; }
        public bool IsCreature() { return GetHigh() == HighGuid.Creature; }
        public bool IsPet() { return GetHigh() == HighGuid.Pet; }
        public bool IsVehicle() { return GetHigh() == HighGuid.Vehicle; }
        public bool IsCreatureOrPet() { return IsCreature() || IsPet(); }
        public bool IsCreatureOrVehicle() { return IsCreature() || IsVehicle(); }
        public bool IsAnyTypeCreature() { return IsCreature() || IsPet() || IsVehicle(); }
        public bool IsPlayer() { return !IsEmpty() && GetHigh() == HighGuid.Player; }
        public bool IsUnit() { return IsAnyTypeCreature() || IsPlayer(); }
        public bool IsItem() { return GetHigh() == HighGuid.Item; }
        public bool IsGameObject() { return GetHigh() == HighGuid.GameObject; }
        public bool IsDynamicObject() { return GetHigh() == HighGuid.DynamicObject; }
        public bool IsCorpse() { return GetHigh() == HighGuid.Corpse; }
        public bool IsAreaTrigger() { return GetHigh() == HighGuid.AreaTrigger; }
        public bool IsMOTransport() { return GetHigh() == HighGuid.Transport; }
        public bool IsAnyTypeGameObject() { return IsGameObject() || IsMOTransport(); }
        public bool IsParty() { return GetHigh() == HighGuid.Party; }
        public bool IsGuild() { return GetHigh() == HighGuid.Guild; }
        public bool IsSceneObject() { return GetHigh() == HighGuid.SceneObject; }
        public bool IsConversation() { return GetHigh() == HighGuid.Conversation; }
        public bool IsCast() { return GetHigh() == HighGuid.Cast; }

        public TypeId GetTypeId() { return GetTypeId(GetHigh()); }
        bool HasEntry() { return HasEntry(GetHigh()); }

        public static bool operator <(ObjectGuid left, ObjectGuid right)
        {
            if (left._high < right._high)
                return true;
            else if (left._high > right._high)
                return false;

            return left._low < right._low;
        }
        public static bool operator >(ObjectGuid left, ObjectGuid right)
        {
            if (left._high > right._high)
                return true;
            else if (left._high < right._high)
                return false;

            return left._low > right._low;
        }

        public override string ToString()
        {
            string str = $"GUID Full: 0x{_high + _low}, Type: {GetHigh()}";
            if (HasEntry())
                str += (IsPet() ? " Pet number: " : " Entry: ") + GetEntry() + " ";

            str += " Low: " + GetCounter();
            return str;
        }

        public static ObjectGuid FromString(string guidString)
        {
            return ObjectGuidInfo.Parse(guidString);
        }

        public static bool operator ==(ObjectGuid first, ObjectGuid other)
        {
            return first.Equals(other);
        }

        public static bool operator !=(ObjectGuid first, ObjectGuid other)
        {
            return !(first == other);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is ObjectGuid && Equals((ObjectGuid)obj);
        }

        public bool Equals(ObjectGuid other)
        {
            return other._high == _high && other._low == _low;
        }

        public override int GetHashCode()
        {
            return new { _high, _low }.GetHashCode();
        }

        //Static Methods 
        static TypeId GetTypeId(HighGuid high)
        {
            switch (high)
            {
                case HighGuid.Item:
                    return TypeId.Item;
                case HighGuid.Creature:
                case HighGuid.Pet:
                case HighGuid.Vehicle:
                    return TypeId.Unit;
                case HighGuid.Player:
                    return TypeId.Player;
                case HighGuid.GameObject:
                case HighGuid.Transport:
                    return TypeId.GameObject;
                case HighGuid.DynamicObject:
                    return TypeId.DynamicObject;
                case HighGuid.Corpse:
                    return TypeId.Corpse;
                case HighGuid.AreaTrigger:
                    return TypeId.AreaTrigger;
                case HighGuid.SceneObject:
                    return TypeId.SceneObject;
                case HighGuid.Conversation:
                    return TypeId.Conversation;
                default:
                    return TypeId.Object;
            }
        }
        static bool HasEntry(HighGuid high)
        {
            switch (high)
            {
                case HighGuid.GameObject:
                case HighGuid.Creature:
                case HighGuid.Pet:
                case HighGuid.Vehicle:
                default:
                    return true;
            }
        }
        public static bool IsMapSpecific(HighGuid high)
        {
            switch (high)
            {
                case HighGuid.Conversation:
                case HighGuid.Creature:
                case HighGuid.Vehicle:
                case HighGuid.Pet:
                case HighGuid.GameObject:
                case HighGuid.DynamicObject:
                case HighGuid.AreaTrigger:
                case HighGuid.Corpse:
                case HighGuid.LootObject:
                case HighGuid.SceneObject:
                case HighGuid.Scenario:
                case HighGuid.AIGroup:
                case HighGuid.DynamicDoor:
                case HighGuid.Vignette:
                case HighGuid.CallForHelp:
                case HighGuid.AIResource:
                case HighGuid.AILock:
                case HighGuid.AILockTicket:
                    return true;
                default:
                    return false;
            }
        }
        public static bool IsRealmSpecific(HighGuid high)
        {
            switch (high)
            {
                case HighGuid.Player:
                case HighGuid.Item:
                case HighGuid.ChatChannel:
                case HighGuid.Transport:
                case HighGuid.Guild:
                    return true;
                default:
                    return false;
            }
        }
        public static bool IsGlobal(HighGuid high)
        {
            switch (high)
            {
                case HighGuid.Uniq:
                case HighGuid.Party:
                case HighGuid.WowAccount:
                case HighGuid.BNetAccount:
                case HighGuid.GMTask:
                case HighGuid.RaidGroup:
                case HighGuid.Spell:
                case HighGuid.Mail:
                case HighGuid.UserRouter:
                case HighGuid.PVPQueueGroup:
                case HighGuid.UserClient:
                case HighGuid.UniqUserClient:
                case HighGuid.BattlePet:
                    return true;
                default:
                    return false;
            }
        }
    }

    public class ObjectGuidGenerator
    {
        long _nextGuid;
        HighGuid _highGuid;

        public ObjectGuidGenerator(HighGuid highGuid, long start = 1)
        {
            _highGuid = highGuid;
            _nextGuid = start;
        }

        public void Set(long val) { _nextGuid = val; }

        public long Generate()
        {
            if (_nextGuid >= ObjectGuid.GetMaxCounter(_highGuid) - 1)
                HandleCounterOverflow();

            if (_highGuid == HighGuid.Creature || _highGuid == HighGuid.Vehicle || _highGuid == HighGuid.GameObject || _highGuid == HighGuid.Transport)
                CheckGuidTrigger(_nextGuid);

            return _nextGuid++;
        }

        public long GetNextAfterMaxUsed() { return _nextGuid; }

        void HandleCounterOverflow()
        {
            Log.outFatal(LogFilter.Server, "{0} guid overflow!! Can't continue, shutting down server. ", _highGuid);
            Global.WorldMgr.StopNow();
        }

        void CheckGuidTrigger(long guidlow)
        {
            if (!Global.WorldMgr.IsGuidAlert() && guidlow > WorldConfig.GetInt64Value(WorldCfg.RespawnGuidAlertLevel))
                Global.WorldMgr.TriggerGuidAlert();
            else if (!Global.WorldMgr.IsGuidWarning() && guidlow > WorldConfig.GetInt64Value(WorldCfg.RespawnGuidWarnLevel))
                Global.WorldMgr.TriggerGuidWarning();
        }
    }

    class ObjectGuidFactory
    {
        public static ObjectGuid CreateNull()
        {
            return new ObjectGuid();
        }

        public static ObjectGuid CreateUniq(long id)
        {
            return new ObjectGuid((long)HighGuid.Uniq << 58, id);
        }

        public static ObjectGuid CreatePlayer(int realmId, long dbId)
        {
            return new ObjectGuid(((long)HighGuid.Player << 58) | ((long)GetRealmIdForObjectGuid(realmId) << 42), dbId);
        }

        public static ObjectGuid CreateItem(int realmId, long dbId)
        {
            return new ObjectGuid(((long)HighGuid.Item << 58) | ((long)GetRealmIdForObjectGuid(realmId) << 42), dbId);
        }

        public static ObjectGuid CreateWorldObject(HighGuid type, byte subType, int realmId, ushort mapId, int serverId, int entry, long counter)
        {
            return new ObjectGuid(((long)type << 58) | (((long)GetRealmIdForObjectGuid(realmId) & 0x1FFF) << 42) | (((long)mapId & 0x1FFF) << 29) | (((long)entry & 0x7FFFFF) << 6) | ((long)subType & 0x3F), 
                (((long)serverId & 0xFFFFFF) << 40) | (counter & 0xFFFFFFFFFF));
        }

        public static ObjectGuid CreateTransport(HighGuid type, long counter)
        {
            return new ObjectGuid(((long)type << 58) | (counter << 38), 0);
        }

        public static ObjectGuid CreateClientActor(ushort ownerType, ushort ownerId, uint counter)
        {
            return new ObjectGuid(((long)HighGuid.ClientActor << 58) | (((long)ownerType & 0x1FFF) << 42) | (((long)ownerId & 0xFFFFFF) << 26), counter);
        }

        public static ObjectGuid CreateChatChannel(int realmId, bool builtIn, bool trade, ushort zoneId, byte factionGroupMask, long counter)
        {
            return new ObjectGuid(((long)HighGuid.ChatChannel << 58) | (((long)GetRealmIdForObjectGuid(realmId) & 0x1FFF) << 42) | ((long)(builtIn ? 1 : 0) << 25) | ((long)(trade ? 1 : 0) << 24) | (((long)zoneId & 0x3FFF) << 10) | (((long)factionGroupMask & 0x3F) << 4), counter);
        }

        public static ObjectGuid CreateGlobal(HighGuid type, long dbIdHigh, long dbId)
        {
            return new ObjectGuid(((long)type << 58) | (dbIdHigh & 0x3FFFFFFFFFFFFFF), dbId);
        }

        public static ObjectGuid CreateGuild(HighGuid type, int realmId, long dbId)
        {
            return new ObjectGuid(((long)type << 58) | ((long)GetRealmIdForObjectGuid(realmId) << 42), dbId);
        }

        public static ObjectGuid CreateMobileSession(int realmId, ushort arg1, long counter)
        {
            return new ObjectGuid(((long)HighGuid.MobileSession << 58) | ((long)GetRealmIdForObjectGuid(realmId) << 42) | (((long)arg1 & 0x1FF) << 33), counter);
        }

        public static ObjectGuid CreateWebObj(int realmId, byte arg1, byte arg2, long counter)
        {
            return new ObjectGuid(((long)HighGuid.WebObj << 58) | (((long)GetRealmIdForObjectGuid(realmId) & 0x1FFF) << 42) | (((long)arg1 & 0x1F) << 37) | (((long)arg2 & 0x3) << 35), counter);
        }

        public static ObjectGuid CreateLFGObject(byte arg1, byte arg2, byte arg3, byte arg4, bool arg5, byte arg6, long counter)
        {
            return new ObjectGuid(((long)HighGuid.LFGObject << 58) | (((long)arg1 & 0xF) << 54) | (((long)arg2 & 0xF) << 50) | (((long)arg3 & 0xF) << 46) | (((long)arg4 & 0xFF) << 38) | ((long)(arg5 ? 1 : 0) << 37) | (((long)arg6 & 0x3) << 35), counter);
        }

        public static ObjectGuid CreateLFGList(byte arg1, long counter)
        {
            return new ObjectGuid(((long)HighGuid.LFGObject << 58) | (((long)arg1 & 0xF) << 54), counter);
        }

        public static ObjectGuid CreateClient(HighGuid type, int realmId, int arg1, long counter)
        {
            return new ObjectGuid(((long)type << 58) | (((long)GetRealmIdForObjectGuid(realmId) & 0x1FFF) << 42) | ((arg1 & 0xFFFFFFFF) << 10), counter);
        }

        public static ObjectGuid CreateClubFinder(int realmId, byte type, int clubFinderId, long dbId)
        {
            return new ObjectGuid(((long)HighGuid.ClubFinder << 58) | (type == 1 ? (((long)GetRealmIdForObjectGuid(realmId) & 0x1FFF) << 42) : 0) | (((long)type & 0xFF) << 33) | (clubFinderId & 0xFFFFFFFF), dbId);
        }

        public static ObjectGuid CreateToolsClient(int mapId, int serverId, long counter)
        {
            return new ObjectGuid(((long)HighGuid.ToolsClient << 58) | (long)mapId, ((long)(serverId & 0xFFFFFF) << 40) | (counter & 0xFFFFFFFFFF));
        }

        public static ObjectGuid CreateWorldLayer(uint arg1, ushort arg2, byte arg3, uint arg4)
        {
            return new ObjectGuid(((long)HighGuid.WorldLayer << 58) | (((long)arg1 & 0xFFFFFFFF) << 10) | ((long)arg2 & 0x1FFu), (((long)arg3 & 0xFF) << 24) | ((long)arg4 & 0x7FFFFF));
        }

        public static ObjectGuid CreateLMMLobby(int realmId, int arg2, byte arg3, byte arg4, long counter)
        {
            return new ObjectGuid(((long)HighGuid.LMMLobby << 58)
                | ((long)GetRealmIdForObjectGuid(realmId) << 42)
                | ((arg2 & 0xFFFFFFFF) << 26)
                | (((long)arg3 & 0xFF) << 18)
                | (((long)arg4 & 0xFF) << 10),
                counter);
        }

        static int GetRealmIdForObjectGuid(int realmId)
        {
            if (realmId != 0)
                return realmId;

            return Global.WorldMgr.GetRealmId().Index;
        }
    }

    class ObjectGuidInfo
    {
        static Dictionary<HighGuid, string> Names = new();
        static Dictionary<HighGuid, Func<HighGuid, ObjectGuid, string>> ClientFormatFunction = new();
        static Dictionary<HighGuid, Func<HighGuid, string, ObjectGuid>> ClientParseFunction = new();

        static ObjectGuidInfo()
        {
            SET_GUID_INFO(HighGuid.Null, FormatNull, ParseNull);
            SET_GUID_INFO(HighGuid.Uniq, FormatUniq, ParseUniq);
            SET_GUID_INFO(HighGuid.Player, FormatPlayer, ParsePlayer);
            SET_GUID_INFO(HighGuid.Item, FormatItem, ParseItem);
            SET_GUID_INFO(HighGuid.WorldTransaction, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.StaticDoor, FormatTransport, ParseTransport);
            SET_GUID_INFO(HighGuid.Transport, FormatTransport, ParseTransport);
            SET_GUID_INFO(HighGuid.Conversation, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.Creature, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.Vehicle, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.Pet, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.GameObject, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.DynamicObject, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.AreaTrigger, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.Corpse, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.LootObject, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.SceneObject, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.Scenario, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.AIGroup, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.DynamicDoor, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.ClientActor, FormatClientActor, ParseClientActor);
            SET_GUID_INFO(HighGuid.Vignette, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.CallForHelp, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.AIResource, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.AILock, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.AILockTicket, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.ChatChannel, FormatChatChannel, ParseChatChannel);
            SET_GUID_INFO(HighGuid.Party, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.Guild, FormatGuild, ParseGuild);
            SET_GUID_INFO(HighGuid.WowAccount, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.BNetAccount, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.GMTask, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.MobileSession, FormatMobileSession, ParseMobileSession);
            SET_GUID_INFO(HighGuid.RaidGroup, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.Spell, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.Mail, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.WebObj, FormatWebObj, ParseWebObj);
            SET_GUID_INFO(HighGuid.LFGObject, FormatLFGObject, ParseLFGObject);
            SET_GUID_INFO(HighGuid.LFGList, FormatLFGList, ParseLFGList);
            SET_GUID_INFO(HighGuid.UserRouter, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.PVPQueueGroup, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.UserClient, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.PetBattle, FormatClient, ParseClient);
            SET_GUID_INFO(HighGuid.UniqUserClient, FormatClient, ParseClient);
            SET_GUID_INFO(HighGuid.BattlePet, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.CommerceObj, FormatGlobal, ParseGlobal);
            SET_GUID_INFO(HighGuid.ClientSession, FormatClient, ParseClient);
            SET_GUID_INFO(HighGuid.Cast, FormatWorldObject, ParseWorldObject);
            SET_GUID_INFO(HighGuid.ClientConnection, FormatClient, ParseClient);
            SET_GUID_INFO(HighGuid.ClubFinder, FormatClubFinder, ParseClubFinder);
            SET_GUID_INFO(HighGuid.ToolsClient, FormatToolsClient, ParseToolsClient);
            SET_GUID_INFO(HighGuid.WorldLayer, FormatWorldLayer, ParseWorldLayer);
            SET_GUID_INFO(HighGuid.ArenaTeam, FormatGuild, ParseGuild);
            SET_GUID_INFO(HighGuid.LMMParty, FormatClient, ParseClient);
            SET_GUID_INFO(HighGuid.LMMLobby, FormatLMMLobby, ParseLMMLobby);
        }

        static void SET_GUID_INFO(HighGuid type, Func<HighGuid, ObjectGuid, string> format, Func<HighGuid, string, ObjectGuid> parse)
        {
            Names[type] = type.ToString();
            ClientFormatFunction[type] = format;
            ClientParseFunction[type] = parse;
        }

        public static string Format(ObjectGuid guid)
        {
            if (guid.GetHigh() >= HighGuid.Count)
                return "Uniq-WOWGUID_TO_STRING_FAILED";

            if (ClientFormatFunction[guid.GetHigh()] == null)
                return "Uniq-WOWGUID_TO_STRING_FAILED";

            return ClientFormatFunction[guid.GetHigh()](guid.GetHigh(), guid);
        }

        public static ObjectGuid Parse(string guidString)
        {
            int typeEnd = guidString.IndexOf('-');
            if (typeEnd == -1)
                return ObjectGuid.FromStringFailed;

            if (!Enum.TryParse<HighGuid>(guidString.Substring(0, typeEnd), out HighGuid type))
                return ObjectGuid.FromStringFailed;

            if (type >= HighGuid.Count)
                return ObjectGuid.FromStringFailed;

            return ClientParseFunction[type](type, guidString.Substring(typeEnd + 1));
        }

        static string FormatNull(HighGuid typeName, ObjectGuid guid)
        {
            return "0000000000000000";
        }

        static ObjectGuid ParseNull(HighGuid type, string guidString)
        {
            return ObjectGuid.Empty;
        }

        static string FormatUniq(HighGuid typeName, ObjectGuid guid)
        {
            string[] uniqNames =
            [
                null,
                "WOWGUID_UNIQUE_PROBED_DELETE",
                "WOWGUID_UNIQUE_JAM_TEMP",
                "WOWGUID_TO_STRING_FAILED",
                "WOWGUID_FROM_STRING_FAILED",
                "WOWGUID_UNIQUE_SERVER_SELF",
                "WOWGUID_UNIQUE_MAGIC_SELF",
                "WOWGUID_UNIQUE_MAGIC_PET",
                "WOWGUID_UNIQUE_INVALID_TRANSPORT",
                "WOWGUID_UNIQUE_AMMO_ID",
                "WOWGUID_SPELL_TARGET_TRADE_ITEM",
                "WOWGUID_SCRIPT_TARGET_INVALID",
                "WOWGUID_SCRIPT_TARGET_NONE",
                null,
                "WOWGUID_FAKE_MODERATOR",
                null,
                null,
                "WOWGUID_UNIQUE_ACCOUNT_OBJ_INITIALIZATION"
            ];

            long id = guid.GetCounter();
            if ((int)id >= uniqNames.Length)
                id = 3;

            return $"{typeName}-{uniqNames[id]}";
        }

        static ObjectGuid ParseUniq(HighGuid type, string guidString)
        {
            string[] uniqNames =
            [
                null,
                "WOWGUID_UNIQUE_PROBED_DELETE",
                "WOWGUID_UNIQUE_JAM_TEMP",
                "WOWGUID_TO_STRING_FAILED",
                "WOWGUID_FROM_STRING_FAILED",
                "WOWGUID_UNIQUE_SERVER_SELF",
                "WOWGUID_UNIQUE_MAGIC_SELF",
                "WOWGUID_UNIQUE_MAGIC_PET",
                "WOWGUID_UNIQUE_INVALID_TRANSPORT",
                "WOWGUID_UNIQUE_AMMO_ID",
                "WOWGUID_SPELL_TARGET_TRADE_ITEM",
                "WOWGUID_SCRIPT_TARGET_INVALID",
                "WOWGUID_SCRIPT_TARGET_NONE",
                null,
                "WOWGUID_FAKE_MODERATOR",
                null,
                null,
                "WOWGUID_UNIQUE_ACCOUNT_OBJ_INITIALIZATION"
            ];

            for (int id = 0; id < uniqNames.Length; ++id)
            {
                if (uniqNames[id] == null)
                    continue;

                if (guidString.Equals(uniqNames[id]))
                    return ObjectGuidFactory.CreateUniq(id);
            }

            return ObjectGuid.FromStringFailed;
        }

        static string FormatPlayer(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-0x{guid.GetLowValue():X16}";
        }

        static ObjectGuid ParsePlayer(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 2)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !long.TryParse(split[1], out long dbId))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreatePlayer(realmId, dbId);
        }

        static string FormatItem(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 18) & 0xFFFFFF}-0x{guid.GetLowValue():X16}";
        }

        static ObjectGuid ParseItem(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 3)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !int.TryParse(split[1], out int arg1) || !long.TryParse(split[2], out long dbId))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateItem(realmId, dbId);
        }

        static string FormatWorldObject(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetSubType()}-{guid.GetRealmId()}-{guid.GetMapId()}-{(guid.GetLowValue() >> 40) & 0xFFFFFF}-{guid.GetEntry()}-0x{guid.GetCounter():X10}";
        }

        static ObjectGuid ParseWorldObject(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 6)
                return ObjectGuid.FromStringFailed;

            if (!byte.TryParse(split[0], out byte subType) || !int.TryParse(split[1], out int realmId) || !ushort.TryParse(split[2], out ushort mapId) ||
                !int.TryParse(split[3], out int serverId) || !int.TryParse(split[4], out int id) || !long.TryParse(split[5], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateWorldObject(type, subType, realmId, mapId, serverId, id, counter);
        }

        static string FormatTransport(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{(guid.GetHighValue() >> 38) & 0xFFFFF}-0x{guid.GetLowValue():X16}";
        }

        static ObjectGuid ParseTransport(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 2)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int id) || !long.TryParse(split[1], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateTransport(type, counter);
        }

        static string FormatClientActor(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 26) & 0xFFFFFF}-{guid.GetLowValue()}";
        }

        static ObjectGuid ParseClientActor(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 3)
                return ObjectGuid.FromStringFailed;

            if (!ushort.TryParse(split[0], out ushort ownerType) || !ushort.TryParse(split[1], out ushort ownerId) || !uint.TryParse(split[2], out uint counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateClientActor(ownerType, ownerId, counter);
        }

        static string FormatChatChannel(HighGuid typeName, ObjectGuid guid)
        {
            int builtIn = (int)(guid.GetHighValue() >> 25) & 0x1;
            int trade = (int)(guid.GetHighValue() >> 24) & 0x1;
            int zoneId = (int)(guid.GetHighValue() >> 10) & 0x3FFF;
            int factionGroupMask = (int)(guid.GetHighValue() >> 4) & 0x3F;
            return $"{typeName}-{guid.GetRealmId()}-{builtIn}-{trade}-{zoneId}-{factionGroupMask}-0x{guid.GetLowValue():X8}";
        }

        static ObjectGuid ParseChatChannel(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 6)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !int.TryParse(split[1], out int builtIn) || !int.TryParse(split[2], out int trade) ||
                !ushort.TryParse(split[3], out ushort zoneId) || !byte.TryParse(split[4], out byte factionGroupMask) || !long.TryParse(split[5], out long id))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateChatChannel(realmId, builtIn != 0, trade != 0, zoneId, factionGroupMask, id);
        }

        static string FormatGlobal(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetHighValue() & 0x3FFFFFFFFFFFFFF}-0x{guid.GetLowValue():X12}";
        }

        static ObjectGuid ParseGlobal(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 2)
                return ObjectGuid.FromStringFailed;

            if (!long.TryParse(split[0], out long dbIdHigh) || !long.TryParse(split[1], out long dbIdLow))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateGlobal(type, dbIdHigh, dbIdLow);
        }

        static string FormatGuild(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-0x{guid.GetLowValue():X12}";
        }

        static ObjectGuid ParseGuild(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 2)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !long.TryParse(split[1], out long dbId))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateGuild(type, realmId, dbId);
        }

        static string FormatMobileSession(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 33) & 0x1FF}-0x{guid.GetLowValue():X8}";
        }

        static ObjectGuid ParseMobileSession(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 3)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !ushort.TryParse(split[1], out ushort arg1) || !long.TryParse(split[2], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateMobileSession(realmId, arg1, counter);
        }

        static string FormatWebObj(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 37) & 0x1F}-{(guid.GetHighValue() >> 35) & 0x3}-0x{guid.GetLowValue():X12}";
        }

        static ObjectGuid ParseWebObj(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 4)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !byte.TryParse(split[1], out byte arg1) || !byte.TryParse(split[2], out byte arg2) || !long.TryParse(split[3], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateWebObj(realmId, arg1, arg2, counter);
        }

        static string FormatLFGObject(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{(guid.GetHighValue() >> 54) & 0xF}-{(guid.GetHighValue() >> 50) & 0xF}-{(guid.GetHighValue() >> 46) & 0xF}-" +
                $"{(guid.GetHighValue() >> 38) & 0xFF}-{(guid.GetHighValue() >> 37) & 0x1}-{(guid.GetHighValue() >> 35) & 0x3}-0x{guid.GetLowValue():X6}";
        }

        static ObjectGuid ParseLFGObject(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 7)
                return ObjectGuid.FromStringFailed;

            if (!byte.TryParse(split[0], out byte arg1) || !byte.TryParse(split[1], out byte arg2) || !byte.TryParse(split[2], out byte arg3) ||
                !byte.TryParse(split[3], out byte arg4) || !byte.TryParse(split[4], out byte arg5) || !byte.TryParse(split[5], out byte arg6) || !long.TryParse(split[6], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateLFGObject(arg1, arg2, arg3, arg4, arg5 != 0, arg6, counter);
        }

        static string FormatLFGList(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{(guid.GetHighValue() >> 54) & 0xF}-0x{guid.GetLowValue():X6}";
        }

        static ObjectGuid ParseLFGList(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 2)
                return ObjectGuid.FromStringFailed;

            if (!byte.TryParse(split[0], out byte arg1) || !long.TryParse(split[1], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateLFGList(arg1, counter);
        }

        static string FormatClient(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 10) & 0xFFFFFFFF}-0x{guid.GetLowValue():X12}";
        }

        static ObjectGuid ParseClient(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 3)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !int.TryParse(split[1], out int arg1) || !long.TryParse(split[2], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateClient(type, realmId, arg1, counter);
        }

        static string FormatClubFinder(HighGuid typeName, ObjectGuid guid)
        {
            uint type = (uint)(guid.GetHighValue() >> 33) & 0xFF;
            uint clubFinderId = (uint)(guid.GetHighValue() & 0xFFFFFFFF);
            if (type == 1) // guild
                return $"{typeName}-{type}-{clubFinderId}-{guid.GetRealmId()}-{guid.GetLowValue()}";

            return $"{typeName}-{type}-{clubFinderId}-0x{guid.GetLowValue():X16}";
        }

        static ObjectGuid ParseClubFinder(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length < 1)
                return ObjectGuid.FromStringFailed;

            if (!byte.TryParse(split[0], out byte typeNum))
                return ObjectGuid.FromStringFailed;

            int clubFinderId = 0;
            int realmId = 0;
            long dbId = 0;

            switch (typeNum)
            {
                case 0: // club
                    if (split.Length < 3)
                        return ObjectGuid.FromStringFailed;
                    if (!int.TryParse(split[0], out clubFinderId) || !long.TryParse(split[1], out dbId))
                        return ObjectGuid.FromStringFailed;
                    break;
                case 1: // guild
                    if (split.Length < 4)
                        return ObjectGuid.FromStringFailed;
                    if (!int.TryParse(split[0], out clubFinderId) || !int.TryParse(split[1], out realmId) || !long.TryParse(split[2], out dbId))
                        return ObjectGuid.FromStringFailed;
                    break;
                default:
                    return ObjectGuid.FromStringFailed;
            }

            return ObjectGuidFactory.CreateClubFinder(realmId, typeNum, clubFinderId, dbId);
        }

        static string FormatToolsClient(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetMapId()}-{(guid.GetLowValue() >> 40) & 0xFFFFFF}-{guid.GetCounter():X10}";
        }

        static ObjectGuid ParseToolsClient(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 3)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int mapId) || !int.TryParse(split[1], out int serverId) || !long.TryParse(split[2], out long counter))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateToolsClient(mapId, serverId, counter);
        }

        static string FormatWorldLayer(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{((guid.GetHighValue() >> 10) & 0xFFFFFFFF)}-{(guid.GetHighValue() & 0x1FF)}-{((guid.GetLowValue() >> 24) & 0xFF)}-{(guid.GetLowValue() & 0x7FFFFF)}";
        }

        static ObjectGuid ParseWorldLayer(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 4)
                return ObjectGuid.FromStringFailed;

            if (!uint.TryParse(split[0], out uint arg1) || !ushort.TryParse(split[1], out ushort arg2) || !byte.TryParse(split[2], out byte arg3) || !uint.TryParse(split[0], out uint arg4))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateWorldLayer(arg1, arg2, arg3, arg4);
        }

        static string FormatLMMLobby(HighGuid typeName, ObjectGuid guid)
        {
            return $"{typeName}-{guid.GetRealmId()}-{(guid.GetHighValue() >> 26) & 0xFFFFFF}-{(guid.GetHighValue() >> 18) & 0xFF}-{(guid.GetHighValue() >> 10) & 0xFF}-{guid.GetLowValue():X}";
        }

        static ObjectGuid ParseLMMLobby(HighGuid type, string guidString)
        {
            string[] split = guidString.Split('-');
            if (split.Length != 5)
                return ObjectGuid.FromStringFailed;

            if (!int.TryParse(split[0], out int realmId) || !int.TryParse(split[1], out int arg2) || !byte.TryParse(split[2], out byte arg3) || !byte.TryParse(split[0], out byte arg4) || !long.TryParse(split[0], out long arg5))
                return ObjectGuid.FromStringFailed;

            return ObjectGuidFactory.CreateLMMLobby(realmId, arg2, arg3, arg4, arg5);
        }
    }

    public class Legacy
    {
        public enum LegacyTypeId
        {
            Object          = 0,
            Item            = 1,
            Container       = 2,
            Unit            = 3,
            Player          = 4,
            GameObject      = 5,
            DynamicObject   = 6,
            Corpse          = 7,
            AreaTrigger     = 8,
            SceneObject     = 9,
            Conversation    = 10,
            Max
        }

        public static int ConvertLegacyTypeID(int legacyTypeID) => (LegacyTypeId)legacyTypeID switch
        {
            LegacyTypeId.Object => (int)TypeId.Object,
            LegacyTypeId.Item => (int)TypeId.Item,
            LegacyTypeId.Container => (int)TypeId.Container,
            LegacyTypeId.Unit => (int)TypeId.Unit,
            LegacyTypeId.Player => (int)TypeId.Player,
            LegacyTypeId.GameObject => (int)TypeId.GameObject,
            LegacyTypeId.DynamicObject => (int)TypeId.DynamicObject,
            LegacyTypeId.Corpse => (int)TypeId.Corpse,
            LegacyTypeId.AreaTrigger => (int)TypeId.AreaTrigger,
            LegacyTypeId.SceneObject => (int)TypeId.SceneObject,
            LegacyTypeId.Conversation => (int)TypeId.Conversation,
            _ => (int)TypeId.Object
        };

        public static int ConvertLegacyTypeMask(int legacyTypeMask)
        {
            int typeMask = 0;
            for (int i = (int)LegacyTypeId.Object; i < (int)LegacyTypeId.Max; i = i + 1)
                if ((legacyTypeMask & (1 << i)) != 0)
                    typeMask |= 1 << ConvertLegacyTypeID(i);

            return typeMask;
        }
    }
}
