﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.IO;
using Game.Cache;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Networking.Packets
{
    public class QueryCreature : ClientPacket
    {
        public QueryCreature(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            CreatureID = _worldPacket.ReadInt32();
        }

        public int CreatureID;
    }

    public class QueryCreatureResponse : ServerPacket
    {
        public QueryCreatureResponse() : base(ServerOpcodes.QueryCreatureResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(CreatureID);
            _worldPacket.WriteBit(Allow);
            _worldPacket.FlushBits();

            if (Allow)
            {
                _worldPacket.WriteBits(Stats.Title.IsEmpty() ? 0 : Stats.Title.GetByteCount() + 1, 11);
                _worldPacket.WriteBits(Stats.TitleAlt.IsEmpty() ? 0 : Stats.TitleAlt.GetByteCount() + 1, 11);
                _worldPacket.WriteBits(Stats.CursorName.IsEmpty() ? 0 : Stats.CursorName.GetByteCount() + 1, 6);
                _worldPacket.WriteBit(Stats.Civilian);
                _worldPacket.WriteBit(Stats.Leader);

                for (var i = 0; i < SharedConst.MaxCreatureNames; ++i)
                {
                    _worldPacket.WriteBits(Stats.Name[i].GetByteCount() + 1, 11);
                    _worldPacket.WriteBits(Stats.NameAlt[i].GetByteCount() + 1, 11);
                }

                for (var i = 0; i < SharedConst.MaxCreatureNames; ++i)
                {
                    if (!string.IsNullOrEmpty(Stats.Name[i]))
                        _worldPacket.WriteCString(Stats.Name[i]);
                    if (!string.IsNullOrEmpty(Stats.NameAlt[i]))
                        _worldPacket.WriteCString(Stats.NameAlt[i]);
                }

                for (var i = 0; i < 2; ++i)
                    _worldPacket.WriteUInt32(Stats.Flags[i]);

                _worldPacket.WriteInt32((int)Stats.CreatureType);
                _worldPacket.WriteInt32((int)Stats.CreatureFamily);
                _worldPacket.WriteInt32((int)Stats.Classification);
                _worldPacket.WriteInt32(Stats.PetSpellDataId);

                for (var i = 0; i < SharedConst.MaxCreatureKillCredit; ++i)
                    _worldPacket.WriteInt32(Stats.ProxyCreatureID[i]);

                _worldPacket.WriteInt32(Stats.Display.CreatureDisplay.Count);
                _worldPacket.WriteFloat(Stats.Display.TotalProbability);

                foreach (CreatureXDisplay display in Stats.Display.CreatureDisplay)
                {
                    _worldPacket.WriteInt32(display.CreatureDisplayID);
                    _worldPacket.WriteFloat(display.Scale);
                    _worldPacket.WriteFloat(display.Probability);
                }

                _worldPacket.WriteFloat(Stats.HpMulti);
                _worldPacket.WriteFloat(Stats.EnergyMulti);

                _worldPacket.WriteInt32(Stats.QuestItems.Count);
                _worldPacket.WriteInt32(Stats.CreatureMovementInfoID);
                _worldPacket.WriteInt32((int)Stats.HealthScalingExpansion);
                _worldPacket.WriteInt32((int)Stats.RequiredExpansion);
                _worldPacket.WriteInt32(Stats.VignetteID);
                _worldPacket.WriteInt32((int)Stats.Class);
                _worldPacket.WriteInt32(Stats.CreatureDifficultyID);
                _worldPacket.WriteInt32(Stats.WidgetSetID);
                _worldPacket.WriteInt32(Stats.WidgetSetUnitConditionID);

                if (!Stats.Title.IsEmpty())
                    _worldPacket.WriteCString(Stats.Title);

                if (!Stats.TitleAlt.IsEmpty())
                    _worldPacket.WriteCString(Stats.TitleAlt);

                if (!Stats.CursorName.IsEmpty())
                    _worldPacket.WriteCString(Stats.CursorName);

                foreach (var questItem in Stats.QuestItems)
                    _worldPacket.WriteInt32(questItem);
            }
        }

        public bool Allow;
        public CreatureStats Stats;
        public int CreatureID;
    }

    public class QueryPlayerNames : ClientPacket
    {
        public QueryPlayerNames(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Players = new ObjectGuid[_worldPacket.ReadInt32()];
            for (var i = 0; i < Players.Length; ++i)
                Players[i] = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid[] Players;
    }

    public class QueryPlayerNamesResponse : ServerPacket
    {
        public QueryPlayerNamesResponse() : base(ServerOpcodes.QueryPlayerNamesResponse) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Players.Count);
            foreach (NameCacheLookupResult lookupResult in Players)
                lookupResult.Write(_worldPacket);
        }
        
        public List<NameCacheLookupResult> Players = new();
    }
    
    public class QueryPageText : ClientPacket
    {
        public QueryPageText(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PageTextID = _worldPacket.ReadInt32();
            ItemGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid ItemGUID;
        public int PageTextID;
    }

    public class QueryPageTextResponse : ServerPacket
    {
        public QueryPageTextResponse() : base(ServerOpcodes.QueryPageTextResponse) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(PageTextID);
            _worldPacket.WriteBit(Allow);
            _worldPacket.FlushBits();

            if (Allow)
            {
                _worldPacket.WriteInt32(Pages.Count);
                foreach (PageTextInfo pageText in Pages)
                    pageText.Write(_worldPacket);
            }
        }

        public int PageTextID;
        public bool Allow;
        public List<PageTextInfo> Pages = new();

        public struct PageTextInfo
        {
            public void Write(WorldPacket data)
            {
                data.WriteInt32(Id);
                data.WriteInt32(NextPageID);
                data.WriteInt32(PlayerConditionID);
                data.WriteUInt8(Flags);
                data.WriteBits(Text.GetByteCount(), 12);
                data.FlushBits();

                data.WriteString(Text);
            }

            public int Id;
            public int NextPageID;
            public int PlayerConditionID;
            public byte Flags;
            public string Text;
        }
    }

    public class QueryNPCText : ClientPacket
    {
        public QueryNPCText(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            TextID = _worldPacket.ReadInt32();
            Guid = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Guid;
        public int TextID;
    }

    public class QueryNPCTextResponse : ServerPacket
    {
        public QueryNPCTextResponse() : base(ServerOpcodes.QueryNpcTextResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(TextID);
            _worldPacket.WriteBit(Allow);

            _worldPacket.WriteInt32(Allow ? SharedConst.MaxNpcTextOptions * (4 + 4) : 0);
            if (Allow)
            {
                for (int i = 0; i < SharedConst.MaxNpcTextOptions; ++i)
                    _worldPacket.WriteFloat(Probabilities[i]);

                for (int i = 0; i < SharedConst.MaxNpcTextOptions; ++i)
                    _worldPacket.WriteInt32(BroadcastTextID[i]);
            }
        }

        public int TextID;
        public bool Allow;
        public float[] Probabilities = new float[SharedConst.MaxNpcTextOptions];
        public int[] BroadcastTextID = new int[SharedConst.MaxNpcTextOptions];
    }

    public class QueryGameObject : ClientPacket
    {
        public QueryGameObject(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            GameObjectID = _worldPacket.ReadInt32();
            Guid = _worldPacket.ReadPackedGuid();
        }

        public int GameObjectID;
        public ObjectGuid Guid;
    }

    public class QueryGameObjectResponse : ServerPacket
    {
        public QueryGameObjectResponse() : base(ServerOpcodes.QueryGameObjectResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(GameObjectID);
            _worldPacket.WritePackedGuid(Guid);
            _worldPacket.WriteBit(Allow);
            _worldPacket.FlushBits();

            ByteBuffer statsData = new();
            if (Allow)
            {
                statsData.WriteInt32((int)Stats.Type);
                statsData.WriteInt32(Stats.DisplayID);
                for (int i = 0; i < 4; i++)
                    statsData.WriteCString(Stats.Name[i]);

                statsData.WriteCString(Stats.IconName);
                statsData.WriteCString(Stats.CastBarCaption);
                statsData.WriteCString(Stats.UnkString);

                for (uint i = 0; i < SharedConst.MaxGOData; i++)
                    statsData.WriteInt32(Stats.Data[i]);

                statsData.WriteFloat(Stats.Size);
                statsData.WriteUInt8((byte)Stats.QuestItems.Count);
                foreach (uint questItem in Stats.QuestItems)
                    statsData.WriteUInt32(questItem);

                statsData.WriteInt32(Stats.ContentTuningId);
            }

            _worldPacket.WriteUInt32(statsData.GetSize());
            if (statsData.GetSize() != 0)
                _worldPacket.WriteBytes(statsData);
        }

        public int GameObjectID;
        public ObjectGuid Guid;
        public bool Allow;
        public GameObjectStats Stats;
    }

    public class QueryCorpseLocationFromClient : ClientPacket
    {
        public QueryCorpseLocationFromClient(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Player = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Player;
    }

    public class CorpseLocation : ServerPacket
    {
        public CorpseLocation() : base(ServerOpcodes.CorpseLocation) { }

        public override void Write()
        {
            _worldPacket.WriteBit(Valid);
            _worldPacket.FlushBits();

            _worldPacket.WritePackedGuid(Player);
            _worldPacket.WriteInt32(ActualMapID);
            _worldPacket.WriteVector3(Position);
            _worldPacket.WriteInt32(MapID);
            _worldPacket.WritePackedGuid(Transport);
        }

        public ObjectGuid Player;
        public ObjectGuid Transport;
        public Vector3 Position;
        public int ActualMapID;
        public int MapID;
        public bool Valid;
    }

    public class QueryCorpseTransport : ClientPacket
    {
        public QueryCorpseTransport(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Player = _worldPacket.ReadPackedGuid();
            Transport = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Player;
        public ObjectGuid Transport;
    }

    public class CorpseTransportQuery : ServerPacket
    {
        public CorpseTransportQuery() : base(ServerOpcodes.CorpseTransportQuery) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Player);
            _worldPacket.WriteVector3(Position);
            _worldPacket.WriteFloat(Facing);
        }

        public ObjectGuid Player;
        public Vector3 Position;
        public float Facing;
    }

    public class QueryTime : ClientPacket
    {
        public QueryTime(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class QueryTimeResponse : ServerPacket
    {
        public QueryTimeResponse() : base(ServerOpcodes.QueryTimeResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt64((UnixTime64)CurrentTime);
        }

        public RealmTime CurrentTime;
    }

    public class QuestPOIQuery : ClientPacket
    {
        public QuestPOIQuery(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            MissingQuestCount = _worldPacket.ReadInt32();

            for (byte i = 0; i < MissingQuestCount; ++i)
                MissingQuestPOIs[i] = _worldPacket.ReadInt32();
        }

        public int MissingQuestCount;
        public int[] MissingQuestPOIs = new int[175];
    }

    public class QuestPOIQueryResponse : ServerPacket
    {
        public QuestPOIQueryResponse() : base(ServerOpcodes.QuestPoiQueryResponse) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(QuestPOIDataStats.Count);
            _worldPacket.WriteInt32(QuestPOIDataStats.Count);

            bool useCache = WorldConfig.Values[WorldCfg.CacheDataQueries].Bool;

            foreach (QuestPOIData questPOIData in QuestPOIDataStats)
            {
                if (useCache)
                    _worldPacket.WriteBytes(questPOIData.QueryDataBuffer);
                else
                    questPOIData.Write(_worldPacket);
            }
        }

        public List<QuestPOIData> QuestPOIDataStats = new();
    }

    class QueryQuestCompletionNPCs : ClientPacket
    {
        public QueryQuestCompletionNPCs(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int questCount = _worldPacket.ReadInt32();

            for (int i = 0; i < questCount; ++i)
                QuestCompletionNPCs[i] = _worldPacket.ReadInt32();
        }

        public Array<int> QuestCompletionNPCs = new (100);
    }

    class QuestCompletionNPCResponse : ServerPacket
    {
        public QuestCompletionNPCResponse() : base(ServerOpcodes.QuestCompletionNpcResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(QuestCompletionNPCs.Count);
            foreach (var quest in QuestCompletionNPCs)
            {
                _worldPacket.WriteInt32(quest.QuestID);

                _worldPacket.WriteInt32(quest.NPCs.Count);
                foreach (var npc in quest.NPCs)
                    _worldPacket.WriteInt32(npc);
            }
        }

        public List<QuestCompletionNPC> QuestCompletionNPCs = new();
    }

    class QueryPetName : ClientPacket
    {
        public QueryPetName(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            UnitGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid UnitGUID;
    }

    class QueryPetNameResponse : ServerPacket
    {
        public QueryPetNameResponse() : base(ServerOpcodes.QueryPetNameResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(UnitGUID);
            _worldPacket.WriteBit(Allow);

            if (Allow)
            {
                _worldPacket.WriteBits(Name.GetByteCount(), 8);
                _worldPacket.WriteBit(HasDeclined);

                for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                    _worldPacket.WriteBits(DeclinedNames.Name[i].GetByteCount(), 7);

                for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                    _worldPacket.WriteString(DeclinedNames.Name[i]);

                _worldPacket.WriteInt64(Timestamp);
                _worldPacket.WriteString(Name);
            }

            _worldPacket.FlushBits();
        }

        public ObjectGuid UnitGUID;
        public bool Allow;

        public bool HasDeclined;
        public DeclinedName DeclinedNames = new();
        public UnixTime64 Timestamp;
        public string Name = string.Empty;
    }

    class ItemTextQuery : ClientPacket
    {
        public ItemTextQuery(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Id = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Id;
    }

    class QueryItemTextResponse : ServerPacket
    {
        public QueryItemTextResponse() : base(ServerOpcodes.QueryItemTextResponse) { }

        public override void Write()
        {
            _worldPacket.WriteBit(Valid);
            _worldPacket.WriteBits(Text.GetByteCount(), 13);

            _worldPacket.WriteString(Text);
            _worldPacket.WritePackedGuid(Id);
        }

        public ObjectGuid Id;
        public bool Valid;
        public string Text;
    }

    class QueryRealmName : ClientPacket
    {
        public QueryRealmName(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            VirtualRealmAddress = _worldPacket.ReadUInt32();
        }

        public uint VirtualRealmAddress;
    }

    class RealmQueryResponse : ServerPacket
    {
        public RealmQueryResponse() : base(ServerOpcodes.RealmQueryResponse) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(VirtualRealmAddress);
            _worldPacket.WriteUInt8(LookupState);
            if (LookupState == 0)
                NameInfo.Write(_worldPacket);
        }

        public uint VirtualRealmAddress;
        public byte LookupState;
        public VirtualRealmNameInfo NameInfo;
    }

    //Structs
    public class PlayerGuidLookupHint
    {
        public void Write(WorldPacket data)
        {
            data.WriteBit(VirtualRealmAddress.HasValue);
            data.WriteBit(NativeRealmAddress.HasValue);
            data.FlushBits();

            if (VirtualRealmAddress.HasValue)
                data.WriteUInt32(VirtualRealmAddress.Value);

            if (NativeRealmAddress.HasValue)
                data.WriteUInt32(NativeRealmAddress.Value);
        }

        public uint? VirtualRealmAddress = new(); // current realm (?) (identifier made from the Index, BattleGroup and Region)
        public uint? NativeRealmAddress = new(); // original realm (?) (identifier made from the Index, BattleGroup and Region)
    }

    public class PlayerGuidLookupData
    {
        public bool Initialize(ObjectGuid guid, Player player = null)
        {
            CharacterCacheEntry characterInfo = Global.CharacterCacheStorage.GetCharacterCacheByGuid(guid);
            if (characterInfo == null)
                return false;

            if (player != null)
            {
                Cypher.Assert(player.GetGUID() == guid);

                AccountID = player.GetSession().GetAccountGUID();
                BnetAccountID = player.GetSession().GetBattlenetAccountGUID();
                Name = player.GetName();
                RaceID = player.GetRace();
                Sex = player.GetNativeGender();
                ClassID = player.GetClass();
                Level = (byte)player.GetLevel();

                DeclinedName names = player.GetDeclinedNames();
                if (names != null)
                    DeclinedNames = new(names);
            }
            else
            {
                int accountId = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(guid);
                int bnetAccountId = Global.BNetAccountMgr.GetIdByGameAccount(accountId);

                AccountID = ObjectGuid.Create(HighGuid.WowAccount, accountId);
                BnetAccountID = ObjectGuid.Create(HighGuid.BNetAccount, bnetAccountId);
                Name = characterInfo.Name;
                RaceID = characterInfo.RaceId;
                Sex = characterInfo.Sex;
                ClassID = characterInfo.ClassId;
                Level = characterInfo.Level;
            }

            IsDeleted = characterInfo.IsDeleted;
            GuidActual = guid;
            VirtualRealmAddress = Global.WorldMgr.GetVirtualRealmAddress();

            return true;
        }

        public void Write(WorldPacket data)
        {
            data.WriteBit(IsDeleted);
            data.WriteBits(Name.GetByteCount(), 6);

            for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                data.WriteBits(DeclinedNames.Name[i].GetByteCount(), 7);
            
            for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                data.WriteString(DeclinedNames.Name[i]);

            data.WritePackedGuid(AccountID);
            data.WritePackedGuid(BnetAccountID);
            data.WritePackedGuid(GuidActual);
            data.WriteUInt64(GuildClubMemberID);
            data.WriteUInt32(VirtualRealmAddress);
            data.WriteUInt8((byte)RaceID);
            data.WriteUInt8((byte)Sex);
            data.WriteUInt8((byte)ClassID);
            data.WriteUInt8(Level);
            data.WriteUInt8(Unused915);
            data.WriteString(Name);
        }

        public bool IsDeleted;
        public ObjectGuid AccountID;
        public ObjectGuid BnetAccountID;
        public ObjectGuid GuidActual;
        public string Name = string.Empty;
        public ulong GuildClubMemberID;   // same as bgs.protocol.club.v1.MemberId.unique_id
        public uint VirtualRealmAddress;
        public Race RaceID = Race.None;
        public Gender Sex = Gender.None;
        public Class ClassID = Class.None;
        public byte Level;
        public byte Unused915;
        public DeclinedName DeclinedNames = new();
    }

    public class NameCacheUnused920
    {
        public uint Unused1;
        public ObjectGuid Unused2;
        public string Unused3 = string.Empty;
        
        public void Write(WorldPacket data)
        {
            data.WriteUInt32(Unused1);
            data.WritePackedGuid(Unused2);
            data.WriteBits(Unused3.GetByteCount(), 7);
            data.FlushBits();

            data.WriteString(Unused3);
        }
    }

    public struct NameCacheLookupResult
    {
        public ObjectGuid Player;
        public byte Result; // 0 - full packet, != 0 - only guid
        public PlayerGuidLookupData Data;
        public NameCacheUnused920 Unused920;

        public void Write(WorldPacket data)
        {
            data.WriteUInt8(Result);
            data.WritePackedGuid(Player);
            data.WriteBit(Data != null);
            data.WriteBit(Unused920 != null);
            data.FlushBits();

            if (Data != null)
                Data.Write(data);

            if (Unused920 != null)
                Unused920.Write(data);
        }
    }

    public class CreatureXDisplay
    {
        public CreatureXDisplay(int creatureDisplayID, float displayScale, float probability)
        {
            CreatureDisplayID = creatureDisplayID;
            Scale = displayScale;
            Probability = probability;
        }

        public int CreatureDisplayID;
        public float Scale = 1.0f;
        public float Probability = 1.0f;
    }

    public class CreatureDisplayStats
    {
        public float TotalProbability;
        public List<CreatureXDisplay> CreatureDisplay = new();
    }

    public class CreatureStats
    {
        public string Title;
        public string TitleAlt;
        public string CursorName;
        public CreatureType CreatureType;
        public CreatureFamily CreatureFamily;
        public CreatureClassifications Classification;
        public int PetSpellDataId;
        public CreatureDisplayStats Display = new();
        public float HpMulti;
        public float EnergyMulti;
        public bool Civilian;
        public bool Leader;
        public List<int> QuestItems = new();
        public int CreatureMovementInfoID;
        public Expansion HealthScalingExpansion;
        public Expansion RequiredExpansion;
        public int VignetteID;
        public Class Class;
        public int CreatureDifficultyID;
        public int WidgetSetID;
        public int WidgetSetUnitConditionID;
        public uint[] Flags = new uint[2];
        public int[] ProxyCreatureID = new int[SharedConst.MaxCreatureKillCredit];
        public StringArray Name = new(SharedConst.MaxCreatureNames);
        public StringArray NameAlt = new(SharedConst.MaxCreatureNames);
    }

    public struct DBQueryRecord
    {
        public uint RecordID;
    }

    public class GameObjectStats
    {
        public string[] Name = new string[4];
        public string IconName;
        public string CastBarCaption;
        public string UnkString;
        public GameObjectTypes Type;
        public int DisplayID;
        public int[] Data = new int[SharedConst.MaxGOData];
        public float Size;
        public List<int> QuestItems = new();
        public int ContentTuningId;
    }

    class QuestCompletionNPC
    {
        public int QuestID;
        public List<int> NPCs = new();
    }
}
