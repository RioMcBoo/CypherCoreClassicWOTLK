// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Networking.Packets
{
    class CancelAura : ClientPacket
    {
        public CancelAura(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SpellID = _worldPacket.ReadInt32();
            CasterGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid CasterGUID;
        public int SpellID;
    }

    class CancelAutoRepeatSpell : ClientPacket
    {
        public CancelAutoRepeatSpell(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class CancelChannelling : ClientPacket
    {
        public CancelChannelling(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ChannelSpell = _worldPacket.ReadInt32();
            Reason = (SpellInterruptReason)_worldPacket.ReadInt32();
        }

        public int ChannelSpell;
        public SpellInterruptReason Reason;   
    }

    class CancelGrowthAura : ClientPacket
    {
        public CancelGrowthAura(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class CancelMountAura : ClientPacket
    {
        public CancelMountAura(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class CancelModSpeedNoControlAuras : ClientPacket
    {
        public CancelModSpeedNoControlAuras(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            TargetGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid TargetGUID;
    }

    class PetCancelAura : ClientPacket
    {
        public PetCancelAura(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();
            SpellID = _worldPacket.ReadInt32();
        }

        public ObjectGuid PetGUID;
        public int SpellID;
    }

    public class SendKnownSpells : ServerPacket
    {
        public SendKnownSpells() : base(ServerOpcodes.SendKnownSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteBit(InitialLogin);
            _worldPacket.WriteInt32(KnownSpells.Count);
            _worldPacket.WriteInt32(FavoriteSpells.Count);

            foreach (var spellId in KnownSpells)
                _worldPacket.WriteInt32(spellId);

            foreach (var spellId in FavoriteSpells)
                _worldPacket.WriteInt32(spellId);
        }

        public bool InitialLogin;
        public List<int> KnownSpells = new();
        public List<int> FavoriteSpells = new(); // tradeskill recipes
    }

    public enum ActionsButtonsUpdateReason : byte
    {
        /// <summary>Sends initial action buttons, client does not validate if we have the spell or not</summary>            
        Initialization = 0,
        /// <summary>Used used after spec swaps, client validates if a spell is known</summary> 
        AfterSpecSwap = 1,
        /// <summary>Clears the action bars client sided. This is sent during spec swap before unlearning and before sending the new buttons</summary> 
        SpecSwap = 2,
    }

    public class UpdateActionButtons : ServerPacket
    {
        public UpdateActionButtons() : base(ServerOpcodes.UpdateActionButtons, ConnectionType.Instance) { }

        public override void Write()
        {
            for (var i = 0; i < PlayerConst.MaxActionButtons; ++i)
                _worldPacket.WriteInt64(ActionButtons[i]);

            _worldPacket.WriteUInt8((byte)Reason);
        }

        public long[] ActionButtons = new long[PlayerConst.MaxActionButtons];
        public ActionsButtonsUpdateReason Reason;        
    }

    public class SetActionButton : ClientPacket
    {
        public SetActionButton(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Action = _worldPacket.ReadInt32();
            Index = _worldPacket.ReadUInt8();
        }

        public int Action; // two packed values (action and Type)
        public byte Index;
    }

    public class SendUnlearnSpells : ServerPacket
    {
        public SendUnlearnSpells() : base(ServerOpcodes.SendUnlearnSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Spells.Count);
            foreach (var spell in Spells)
                _worldPacket.WriteInt32(spell);
        }

        List<int> Spells = new();
    }

    public class AuraUpdate : ServerPacket
    {
        public AuraUpdate() : base(ServerOpcodes.AuraUpdate, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteBit(UpdateAll);
            _worldPacket.WriteBits(Auras.Count, 9);
            foreach (AuraInfo aura in Auras)
                aura.Write(_worldPacket);

            _worldPacket.WritePackedGuid(UnitGUID);
        }

        public bool UpdateAll;
        public ObjectGuid UnitGUID;
        public List<AuraInfo> Auras = new();
    }

    public class CastSpell : ClientPacket
    {
        public CastSpell(WorldPacket packet) : base(packet)
        {
            Cast = new SpellCastRequestPkt();
        }

        public override void Read()
        {
            Cast.Read(_worldPacket);
        }
        
        public SpellCastRequestPkt Cast;
    }

    public class PetCastSpell : ClientPacket
    {
        public PetCastSpell(WorldPacket packet) : base(packet)
        {
            Cast = new SpellCastRequestPkt();
        }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();
            Cast.Read(_worldPacket);
        }
        
        public ObjectGuid PetGUID;
        public SpellCastRequestPkt Cast;
    }

    public class UseItem : ClientPacket
    {
        public UseItem(WorldPacket packet) : base(packet)
        {
            Cast = new SpellCastRequestPkt();
        }

        public override void Read()
        {
            PackSlot = _worldPacket.ReadUInt8();
            Slot = _worldPacket.ReadUInt8();
            CastItem = _worldPacket.ReadPackedGuid();
            Cast.Read(_worldPacket);
        }
        
        public byte PackSlot;
        public byte Slot;
        public ObjectGuid CastItem;
        public SpellCastRequestPkt Cast;
    }

    class SpellPrepare : ServerPacket
    {
        public SpellPrepare() : base(ServerOpcodes.SpellPrepare) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ClientCastID);
            _worldPacket.WritePackedGuid(ServerCastID);
        }

        public ObjectGuid ClientCastID;
        public ObjectGuid ServerCastID;
    }

    class SpellGo : CombatLogServerPacket
    {
        public SpellGo() : base(ServerOpcodes.SpellGo, ConnectionType.Instance) { }

        public override void Write()
        {
            Cast.Write(_worldPacket);

            WriteLogDataBit();
            FlushBits();

            WriteLogData();
        }

        public SpellCastData Cast = new();
    }

    public class SpellStart : ServerPacket
    {
        public SpellCastData Cast;

        public SpellStart() : base(ServerOpcodes.SpellStart, ConnectionType.Instance)
        {
            Cast = new SpellCastData();
        }

        public override void Write()
        {
            Cast.Write(_worldPacket);
        }
    }

    public class LearnedSpells : ServerPacket
    {
        public LearnedSpells() : base(ServerOpcodes.LearnedSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(ClientLearnedSpellData.Count);
            _worldPacket.WriteUInt32(SpecializationID);
            _worldPacket.WriteBit(SuppressMessaging);
            _worldPacket.FlushBits();

            foreach (LearnedSpellInfo spell in ClientLearnedSpellData)
                spell.Write(_worldPacket);
        }

        public List<LearnedSpellInfo> ClientLearnedSpellData = new();
        public uint SpecializationID;
        public bool SuppressMessaging;
    }

    public class SupercededSpells : ServerPacket
    {
        public SupercededSpells() : base(ServerOpcodes.SupercededSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(ClientLearnedSpellData.Count);

            foreach (LearnedSpellInfo spell in ClientLearnedSpellData)
                spell.Write(_worldPacket);
        }

        public List<LearnedSpellInfo> ClientLearnedSpellData = new();
    }
    
    public class SpellFailure : ServerPacket
    {
        public SpellFailure() : base(ServerOpcodes.SpellFailure, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterUnit);
            _worldPacket.WritePackedGuid(CastID);
            _worldPacket.WriteInt32(SpellID);
            Visual.Write(_worldPacket);
            _worldPacket.WriteUInt16(Reason);
        }

        public ObjectGuid CasterUnit;
        public int SpellID;
        public SpellCastVisual Visual;
        public ushort Reason;
        public ObjectGuid CastID;
    }

    public class SpellFailedOther : ServerPacket
    {
        public SpellFailedOther() : base(ServerOpcodes.SpellFailedOther, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterUnit);
            _worldPacket.WritePackedGuid(CastID);
            _worldPacket.WriteInt32(SpellID);
            Visual.Write(_worldPacket);
            _worldPacket.WriteUInt8(Reason);
        }

        public ObjectGuid CasterUnit;
        public int SpellID;
        public SpellCastVisual Visual;
        public byte Reason;
        public ObjectGuid CastID;
    }

    class CastFailedBase : ServerPacket
    {
        public CastFailedBase(ServerOpcodes opcode, ConnectionType connectionType) : base(opcode, connectionType) { }

        public override void Write()
        {
            throw new NotImplementedException();
        }
        
        public ObjectGuid CastID;
        public int SpellID;
        public SpellCastResult Reason;
        public int FailedArg1 = -1;
        public int FailedArg2 = -1;
    }

    class CastFailed : CastFailedBase
    {
        public CastFailed() : base(ServerOpcodes.CastFailed, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CastID);
            _worldPacket.WriteInt32(SpellID);
            Visual.Write(_worldPacket);
            _worldPacket.WriteInt32((int)Reason);
            _worldPacket.WriteInt32(FailedArg1);
            _worldPacket.WriteInt32(FailedArg2);
        }
        
        public SpellCastVisual Visual;
    }

    class PetCastFailed : CastFailedBase
    {
        public PetCastFailed() : base(ServerOpcodes.PetCastFailed, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CastID);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteInt32((int)Reason);
            _worldPacket.WriteInt32(FailedArg1);
            _worldPacket.WriteInt32(FailedArg2);
        }
    }

    public class SetSpellModifier : ServerPacket
    {
        public SetSpellModifier(ServerOpcodes opcode) : base(opcode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Modifiers.Count);
            foreach (SpellModifierInfo spellMod in Modifiers)
                spellMod.Write(_worldPacket);
        }

        public List<SpellModifierInfo> Modifiers = new();
    }

    public class UnlearnedSpells : ServerPacket
    {
        public UnlearnedSpells() : base(ServerOpcodes.UnlearnedSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID.Count);
            foreach (var spellId in SpellID)
                _worldPacket.WriteInt32(spellId);

            _worldPacket.WriteBit(SuppressMessaging);
            _worldPacket.FlushBits();
        }

        public List<int> SpellID = new();
        public bool SuppressMessaging;
    }

    public class CooldownEvent : ServerPacket
    {
        public CooldownEvent(bool isPet, int spellId) : base(ServerOpcodes.CooldownEvent, ConnectionType.Instance)
        {
            IsPet = isPet;
            SpellID = spellId;
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
        public int SpellID;
    }

    public class ClearCooldowns : ServerPacket
    {
        public ClearCooldowns() : base(ServerOpcodes.ClearCooldowns, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID.Count);
            foreach (int id in SpellID)
                _worldPacket.WriteInt32(id);

            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public List<int> SpellID = new();
        public bool IsPet;
    }

    public class ClearCooldown : ServerPacket
    {
        public ClearCooldown() : base(ServerOpcodes.ClearCooldown, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteBit(ClearOnHold);
            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
        public int SpellID;
        public bool ClearOnHold;
    }

    public class ModifyCooldown : ServerPacket
    {
        public ModifyCooldown() : base(ServerOpcodes.ModifyCooldown, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteInt32(DeltaTime);
            _worldPacket.WriteBit(IsPet);
            _worldPacket.WriteBit(WithoutCategoryCooldown);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
        public bool WithoutCategoryCooldown;
        public int DeltaTime;
        public int SpellID;
    }

    public class SpellCooldownPkt : ServerPacket
    {
        public SpellCooldownPkt() : base(ServerOpcodes.SpellCooldown, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Caster);
            _worldPacket.WriteUInt8((byte)Flags);
            _worldPacket.WriteInt32(SpellCooldowns.Count);
            foreach (var cooldown in SpellCooldowns)
                cooldown.Write(_worldPacket);
        }

        public List<SpellCooldownStruct> SpellCooldowns = new();
        public ObjectGuid Caster;
        public SpellCooldownFlags Flags;
    }

    public class SendSpellHistory : ServerPacket
    {
        public SendSpellHistory() : base(ServerOpcodes.SendSpellHistory, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Entries.Count);
            foreach (var entry in Entries)
                entry.Write(_worldPacket);
        }

        public List<SpellHistoryEntry> Entries = new();
    }

    public class ClearAllSpellCharges : ServerPacket
    {
        public ClearAllSpellCharges() : base(ServerOpcodes.ClearAllSpellCharges, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
    }

    public class ClearSpellCharges : ServerPacket
    {
        public ClearSpellCharges() : base(ServerOpcodes.ClearSpellCharges, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)Category);
            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
        public SpellCategories Category;
    }

    public class SetSpellCharges : ServerPacket
    {
        public SetSpellCharges() : base(ServerOpcodes.SetSpellCharges) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)Category);
            _worldPacket.WriteUInt32(NextRecoveryTime);
            _worldPacket.WriteUInt8(ConsumedCharges);
            _worldPacket.WriteFloat(ChargeModRate);
            _worldPacket.WriteBit(IsPet);
            _worldPacket.FlushBits();
        }

        public bool IsPet;
        public SpellCategories Category;
        public uint NextRecoveryTime;
        public byte ConsumedCharges;
        public float ChargeModRate = 1.0f;
    }

    public class SendSpellCharges : ServerPacket
    {
        public SendSpellCharges() : base(ServerOpcodes.SendSpellCharges, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Entries.Count);
            foreach (var entry in Entries)
                entry.Write(_worldPacket);
        }

        public List<SpellChargeEntry> Entries = new();
    }

    public class ClearTarget : ServerPacket
    {
        public ClearTarget() : base(ServerOpcodes.ClearTarget) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Guid);
        }

        public ObjectGuid Guid;
    }

    public class CancelOrphanSpellVisual : ServerPacket
    {
        public CancelOrphanSpellVisual() : base(ServerOpcodes.CancelOrphanSpellVisual) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellVisualID);
        }

        public int SpellVisualID;
    }

    public class CancelSpellVisual : ServerPacket
    {
        public CancelSpellVisual() : base(ServerOpcodes.CancelSpellVisual) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Source);
            _worldPacket.WriteInt32(SpellVisualID);
        }

        public ObjectGuid Source;
        public int SpellVisualID;
    }

    class CancelSpellVisualKit : ServerPacket
    {
        public CancelSpellVisualKit() : base(ServerOpcodes.CancelSpellVisualKit) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Source);
            _worldPacket.WriteInt32(SpellVisualKitID);
            _worldPacket.WriteBit(MountedVisual);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Source;
        public int SpellVisualKitID;
        public bool MountedVisual;
    }

    class PlayOrphanSpellVisual : ServerPacket
    {
        public PlayOrphanSpellVisual() : base(ServerOpcodes.PlayOrphanSpellVisual) { }

        public override void Write()
        {
            _worldPacket.WriteVector3(SourceLocation);
            _worldPacket.WriteVector3(SourceRotation);
            _worldPacket.WriteVector3(TargetLocation);
            _worldPacket.WritePackedGuid(Target);
            _worldPacket.WriteInt32(SpellVisualID);
            _worldPacket.WriteFloat(TravelSpeed);
            _worldPacket.WriteFloat(LaunchDelay);
            _worldPacket.WriteFloat(MinDuration);
            _worldPacket.WriteBit(SpeedAsTime);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Target; // Exclusive with TargetLocation
        public Vector3 SourceLocation;
        public int SpellVisualID;
        public bool SpeedAsTime;
        public float TravelSpeed;
        public float LaunchDelay; // Always zero
        public float MinDuration;
        public Vector3 SourceRotation; // Vector of rotations, Orientation is z
        public Vector3 TargetLocation; // Exclusive with Target
    }

    class PlaySpellVisual : ServerPacket
    {
        public PlaySpellVisual() : base(ServerOpcodes.PlaySpellVisual) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Source);
            _worldPacket.WritePackedGuid(Target);
            _worldPacket.WritePackedGuid(Transport);
            _worldPacket.WriteVector3(TargetPosition);
            _worldPacket.WriteInt32(SpellVisualID);
            _worldPacket.WriteFloat(TravelSpeed);
            _worldPacket.WriteUInt16(HitReason);
            _worldPacket.WriteUInt16(MissReason);
            _worldPacket.WriteUInt16(ReflectStatus);
            _worldPacket.WriteFloat(LaunchDelay);
            _worldPacket.WriteFloat(MinDuration);
            _worldPacket.WriteBit(SpeedAsTime);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Source;
        public ObjectGuid Target;
        public ObjectGuid Transport; // Used when Target = Empty && (SpellVisual::Flags & 0x400) == 0
        public Vector3 TargetPosition; // Overrides missile destination for SpellVisual::SpellVisualMissileSetID
        public int SpellVisualID;
        public float TravelSpeed;
        public ushort HitReason;
        public ushort MissReason;
        public ushort ReflectStatus;
        public float LaunchDelay;
        public float MinDuration;
        public bool SpeedAsTime;
    }

    class PlaySpellVisualKit : ServerPacket
    {
        public PlaySpellVisualKit() : base(ServerOpcodes.PlaySpellVisualKit) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Unit);
            _worldPacket.WriteInt32(KitRecID);
            _worldPacket.WriteInt32(KitType);
            _worldPacket.WriteUInt32(Duration);
            _worldPacket.WriteBit(MountedVisual);
            _worldPacket.FlushBits();
        }

        public ObjectGuid Unit;
        public int KitRecID;
        public int KitType;
        public uint Duration;
        public bool MountedVisual;
    }

    class SpellVisualLoadScreen : ServerPacket
    {
        public SpellVisualLoadScreen(int spellVisualKitId, int delay) : base(ServerOpcodes.SpellVisualLoadScreen, ConnectionType.Instance)
        {
            SpellVisualKitID = spellVisualKitId;
            Delay = delay;
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellVisualKitID);
            _worldPacket.WriteInt32(Delay);
        }
        
        public int SpellVisualKitID;
        public int Delay;
    }

    public class CancelCast : ClientPacket
    {
        public CancelCast(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            CastID = _worldPacket.ReadPackedGuid();
            SpellID = _worldPacket.ReadInt32();
        }

        public int SpellID;
        public ObjectGuid CastID;
    }

    public class OpenItem : ClientPacket
    {
        public OpenItem(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Slot = _worldPacket.ReadUInt8();
            PackSlot = _worldPacket.ReadUInt8();
        }

        public byte Slot;
        public byte PackSlot;
    }

    public class SpellChannelStart : ServerPacket
    {
        public SpellChannelStart() : base(ServerOpcodes.SpellChannelStart, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterGUID);
            _worldPacket.WriteInt32(SpellID);
            Visual.Write(_worldPacket);
            _worldPacket.WriteInt32(ChannelDuration);
            _worldPacket.WriteBit(InterruptImmunities.HasValue);
            _worldPacket.WriteBit(HealPrediction.HasValue);
            _worldPacket.FlushBits();

            if (InterruptImmunities.HasValue)
                InterruptImmunities.Value.Write(_worldPacket);

            if (HealPrediction.HasValue)
                HealPrediction.Value.Write(_worldPacket);
        }

        public int SpellID;
        public SpellCastVisual Visual;
        public SpellChannelStartInterruptImmunities? InterruptImmunities;
        public ObjectGuid CasterGUID;
        public SpellTargetedHealPrediction? HealPrediction;
        public Milliseconds ChannelDuration;
    }

    public class SpellChannelUpdate : ServerPacket
    {
        public SpellChannelUpdate() : base(ServerOpcodes.SpellChannelUpdate, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterGUID);
            _worldPacket.WriteInt32(TimeRemaining);
        }

        public ObjectGuid CasterGUID;
        public Milliseconds TimeRemaining;
    }

    class ResurrectRequest : ServerPacket
    {
        public ResurrectRequest() : base(ServerOpcodes.ResurrectRequest) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(ResurrectOffererGUID);
            _worldPacket.WriteUInt32(ResurrectOffererVirtualRealmAddress);
            _worldPacket.WriteInt32(PetNumber);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteBits(Name.GetByteCount(), 11);
            _worldPacket.WriteBit(UseTimer);
            _worldPacket.WriteBit(Sickness);
            _worldPacket.FlushBits();

            _worldPacket.WriteString(Name);
        }

        public ObjectGuid ResurrectOffererGUID;
        public uint ResurrectOffererVirtualRealmAddress;
        public int PetNumber;
        public int SpellID;
        public bool UseTimer;
        public bool Sickness;
        public string Name;
    }

    class UnlearnSkill : ClientPacket
    {
        public UnlearnSkill(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SkillLine = _worldPacket.ReadInt32();
        }

        public int SkillLine;
    }

    class SelfRes : ClientPacket
    {
        public SelfRes(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SpellId = _worldPacket.ReadInt32();
        }

        public int SpellId;
    }

    class GetMirrorImageData : ClientPacket
    {
        public GetMirrorImageData(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            UnitGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid UnitGUID;
    }

    class MirrorImageComponentedData : ServerPacket
    {
        public MirrorImageComponentedData() : base(ServerOpcodes.MirrorImageComponentedData) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(UnitGUID);
            _worldPacket.WriteInt32(DisplayID);
            _worldPacket.WriteInt32(SpellVisualKitID);
            _worldPacket.WriteUInt8(RaceID);
            _worldPacket.WriteUInt8(Gender);
            _worldPacket.WriteUInt8(ClassID);
            _worldPacket.WriteInt32(Customizations.Count);
            _worldPacket.WritePackedGuid(GuildGUID);
            _worldPacket.WriteInt32(ItemDisplayID.Count);

            foreach (ChrCustomizationChoice customization in Customizations)
                customization.Write(_worldPacket);

            foreach (var itemDisplayId in ItemDisplayID)
                _worldPacket.WriteInt32(itemDisplayId);
        }

        public ObjectGuid UnitGUID;
        public int DisplayID;
        public int SpellVisualKitID;
        public byte RaceID;
        public byte Gender;
        public byte ClassID;
        public List<ChrCustomizationChoice> Customizations = new();
        public ObjectGuid GuildGUID;

        public List<int> ItemDisplayID = new();
    }

    class MirrorImageCreatureData : ServerPacket
    {
        public MirrorImageCreatureData() : base(ServerOpcodes.MirrorImageCreatureData) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(UnitGUID);
            _worldPacket.WriteInt32(DisplayID);
            _worldPacket.WriteInt32(SpellVisualKitID);
        }

        public ObjectGuid UnitGUID;
        public int DisplayID;
        public int SpellVisualKitID;
    }

    class SpellClick : ClientPacket
    {
        public SpellClick(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SpellClickUnitGuid = _worldPacket.ReadPackedGuid();
            TryAutoDismount = _worldPacket.HasBit();
        }

        public ObjectGuid SpellClickUnitGuid;
        public bool TryAutoDismount;
    }

    class ResyncRunes : ServerPacket
    {
        public ResyncRunes() : base(ServerOpcodes.ResyncRunes) { }

        public override void Write()
        {
            Runes.Write(_worldPacket);
        }

        public RuneData Runes = new();
    }

    class AddRunePower : ServerPacket
    {
        public AddRunePower() : base(ServerOpcodes.AddRunePower, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteUInt32(AddedRunesMask);
        }

        public uint AddedRunesMask;
    }

    class MissileTrajectoryCollision : ClientPacket
    {
        public MissileTrajectoryCollision(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Target = _worldPacket.ReadPackedGuid();
            SpellID = _worldPacket.ReadInt32();
            CastID = _worldPacket.ReadPackedGuid();
            CollisionPos = _worldPacket.ReadVector3();
        }

        public ObjectGuid Target;
        public int SpellID;
        public ObjectGuid CastID;
        public Vector3 CollisionPos;
    }

    class NotifyMissileTrajectoryCollision : ServerPacket
    {
        public NotifyMissileTrajectoryCollision() : base(ServerOpcodes.NotifyMissileTrajectoryCollision) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Caster);
            _worldPacket.WritePackedGuid(CastID);
            _worldPacket.WriteVector3(CollisionPos);
        }

        public ObjectGuid Caster;
        public ObjectGuid CastID;
        public Vector3 CollisionPos;
    }

    class UpdateMissileTrajectory : ClientPacket
    {
        public UpdateMissileTrajectory(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Guid = _worldPacket.ReadPackedGuid();
            CastID = _worldPacket.ReadPackedGuid();
            MoveMsgID = _worldPacket.ReadUInt16();
            SpellID = _worldPacket.ReadInt32();
            Pitch = _worldPacket.ReadFloat();
            Speed = (Speed)_worldPacket.ReadFloat();
            FirePos = _worldPacket.ReadVector3();
            ImpactPos = _worldPacket.ReadVector3();
            bool hasStatus = _worldPacket.HasBit();

            _worldPacket.ResetBitPos();
            if (hasStatus)
                Status.Read(_worldPacket);
        }

        public ObjectGuid Guid;
        public ObjectGuid CastID;
        public ushort MoveMsgID;
        public int SpellID;
        public float Pitch;
        public Speed Speed;
        public Vector3 FirePos;
        public Vector3 ImpactPos;
        public MovementInfo Status = new();
    }

    public class SpellDelayed : ServerPacket
    {
        public SpellDelayed() : base(ServerOpcodes.SpellDelayed, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(Caster);
            _worldPacket.WriteInt32(ActualDelay);
        }

        public ObjectGuid Caster;
        public int ActualDelay;
    }

    class DispelFailed : ServerPacket
    {
        public DispelFailed() : base(ServerOpcodes.DispelFailed) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterGUID);
            _worldPacket.WritePackedGuid(VictimGUID);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteInt32(FailedSpells.Count);

            foreach (var spell in FailedSpells)
                _worldPacket.WriteInt32(spell);
        }

        public ObjectGuid CasterGUID;
        public ObjectGuid VictimGUID;
        public int SpellID;
        public List<int> FailedSpells = new();
    }

    class CustomLoadScreen : ServerPacket
    {
        public CustomLoadScreen(int teleportSpellId, int loadingScreenId) : base(ServerOpcodes.CustomLoadScreen)
        {
            TeleportSpellID = teleportSpellId;
            LoadingScreenID = loadingScreenId;
        }

        public override void Write()
        {
            _worldPacket.WriteInt32(TeleportSpellID);
            _worldPacket.WriteInt32(LoadingScreenID);
        }

        int TeleportSpellID;
        int LoadingScreenID;
    }

    class MountResultPacket : ServerPacket
    {
        public MountResultPacket() : base(ServerOpcodes.MountResult, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32((int)Result);
        }

        public MountResult Result;
    }

    class MissileCancel : ServerPacket
    {
        public MissileCancel() : base(ServerOpcodes.MissileCancel) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(OwnerGUID);
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteBit(Reverse);
            _worldPacket.FlushBits();
        }

        public ObjectGuid OwnerGUID;
        public bool Reverse;
        public int SpellID;
    }

    class TradeSkillSetFavorite : ClientPacket
    {
        public TradeSkillSetFavorite(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            RecipeID = _worldPacket.ReadInt32();
            IsFavorite = _worldPacket.HasBit();
        }
        
        public int RecipeID;
        public bool IsFavorite;
    }

    class KeyboundOverride : ClientPacket
    {
        public KeyboundOverride(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            OverrideID = _worldPacket.ReadUInt16();
        }

        public ushort OverrideID;
    }

    class CancelQueuedSpell : ClientPacket
    {
        public CancelQueuedSpell(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    public class SpellCategoryCooldown : ServerPacket
    {
        public List<CategoryCooldownInfo> CategoryCooldowns = new();

        public SpellCategoryCooldown() : base(ServerOpcodes.SpellCategoryCooldown, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(CategoryCooldowns.Count);

            foreach (CategoryCooldownInfo cooldown in CategoryCooldowns)
            {
                _worldPacket.WriteUInt32(cooldown.Category);
                _worldPacket.WriteInt32(cooldown.ModCooldown);
            }
        }

        public class CategoryCooldownInfo
        {
            public CategoryCooldownInfo(uint category, int cooldown)
            {
                Category = category;
                ModCooldown = cooldown;
            }

            public uint Category; // SpellCategory Id
            public int ModCooldown; // Reduced Cooldown in ms
        }
    }

    //Structs
    public struct SpellLogPowerData
    {
        public SpellLogPowerData(int powerType, int amount, int cost)
        {
            PowerType = powerType;
            Amount = amount;
            Cost = cost;
        }

        public int PowerType;
        public int Amount;
        public int Cost;
    }

    public class SpellCastLogData
    {
        public void Initialize(Unit unit)
        {
            Health = unit.GetHealth();
            AttackPower = (int)unit.GetTotalAttackPowerValue(unit.GetClass() == Class.Hunter ? WeaponAttackType.RangedAttack : WeaponAttackType.BaseAttack);
            SpellPower = unit.SpellBaseDamageBonusDone(SpellSchoolMask.Spell);
            Armor = unit.GetArmor();
            PowerData.Add(new SpellLogPowerData((int)unit.GetPowerType(), unit.GetPower(unit.GetPowerType()), 0));
        }

        public void Initialize(Spell spell)
        {
            if (spell.GetCaster().ToUnit() is Unit unitCaster)
            {
                Health = unitCaster.GetHealth();
                AttackPower = (int)unitCaster.GetTotalAttackPowerValue(unitCaster.GetClass() == Class.Hunter ? WeaponAttackType.RangedAttack : WeaponAttackType.BaseAttack);
                SpellPower = unitCaster.SpellBaseDamageBonusDone(SpellSchoolMask.Spell);
                Armor = unitCaster.GetArmor();
                PowerType primaryPowerType = unitCaster.GetPowerType();
                bool primaryPowerAdded = false;
                foreach (SpellPowerCost cost in spell.GetPowerCost())
                {
                    PowerData.Add(new SpellLogPowerData((int)cost.Power, unitCaster.GetPower(cost.Power), cost.Amount));
                    if (cost.Power == primaryPowerType)
                        primaryPowerAdded = true;
                }

                if (!primaryPowerAdded)
                    PowerData.Insert(0, new SpellLogPowerData((int)primaryPowerType, unitCaster.GetPower(primaryPowerType), 0));
            }
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt64(Health);
            data.WriteInt32(AttackPower);
            data.WriteInt32(SpellPower);
            data.WriteInt32(Armor);
            data.WriteBits(PowerData.Count, 9);
            data.FlushBits();

            foreach (SpellLogPowerData powerData in PowerData)
            {
                data.WriteInt32(powerData.PowerType);
                data.WriteInt32(powerData.Amount);
                data.WriteInt32(powerData.Cost);
            }
        }

        long Health;
        int AttackPower;
        int SpellPower;
        int Armor;
        List<SpellLogPowerData> PowerData = new();
    }

    class ContentTuningParams
    {
        bool GenerateDataPlayerToPlayer(Player attacker, Player target)
        {
            return false;
        }

        bool GenerateDataCreatureToPlayer(Creature attacker, Player target)
        {
            CreatureTemplate creatureTemplate = attacker.GetCreatureTemplate();
            CreatureDifficulty creatureDifficulty = creatureTemplate.GetDifficulty(attacker.GetMap().GetDifficultyID());

            TuningType = ContentTuningType.CreatureToPlayerDamage;
            PlayerLevelDelta = (short)target.m_activePlayerData.ScalingPlayerLevelDelta;
            PlayerItemLevel = (ushort)target.GetAverageItemLevel();
            TargetItemLevel = 0;
            ScalingHealthItemLevelCurveID = (ushort)target.m_unitData.ScalingHealthItemLevelCurveID;
            TargetLevel = (byte)target.GetLevel();
            Expansion = (byte)creatureDifficulty.HealthScalingExpansion;
            TargetScalingLevelDelta = (sbyte)attacker.m_unitData.ScalingLevelDelta;
            return true;
        }

        bool GenerateDataPlayerToCreature(Player attacker, Creature target)
        {
            CreatureTemplate creatureTemplate = target.GetCreatureTemplate();
            CreatureDifficulty creatureDifficulty = creatureTemplate.GetDifficulty(target.GetMap().GetDifficultyID());

            TuningType = ContentTuningType.PlayerToCreatureDamage;
            PlayerLevelDelta = (short)attacker.m_activePlayerData.ScalingPlayerLevelDelta;
            PlayerItemLevel = (ushort)attacker.GetAverageItemLevel();
            TargetItemLevel = 0;
            ScalingHealthItemLevelCurveID = (ushort)target.m_unitData.ScalingHealthItemLevelCurveID;
            TargetLevel = (byte)target.GetLevel();
            Expansion = (byte)creatureDifficulty.HealthScalingExpansion;
            TargetScalingLevelDelta = (sbyte)target.m_unitData.ScalingLevelDelta;
            return true;
        }

        bool GenerateDataCreatureToCreature(Creature attacker, Creature target)
        {
            Creature accessor = attacker;
            CreatureTemplate creatureTemplate = accessor.GetCreatureTemplate();
            CreatureDifficulty creatureDifficulty = creatureTemplate.GetDifficulty(accessor.GetMap().GetDifficultyID());

            TuningType = ContentTuningType.CreatureToCreatureDamage;
            PlayerLevelDelta = 0;
            PlayerItemLevel = 0;
            TargetLevel = (byte)target.GetLevel();
            Expansion = (byte)creatureDifficulty.HealthScalingExpansion;
            TargetScalingLevelDelta = (sbyte)accessor.m_unitData.ScalingLevelDelta;
            return true;
        }

        public bool GenerateDataForUnits(Unit attacker, Unit target)
        {
            if (WorldObject.ToPlayer(attacker) is Player playerAttacker)
                if (WorldObject.ToPlayer(target) is Player playerTarget)
                    return GenerateDataPlayerToPlayer(playerAttacker, playerTarget);

            return false;
        }

        public void Write(WorldPacket data)
        {
            data.WriteFloat(PlayerItemLevel);
            data.WriteFloat(TargetItemLevel);
            data.WriteInt16(PlayerLevelDelta);
            data.WriteUInt32(ScalingHealthItemLevelCurveID);
            data.WriteUInt8(TargetLevel);
            data.WriteUInt8(Expansion);
            data.WriteInt8(TargetScalingLevelDelta);
            data.WriteUInt32((uint)Flags);
            data.WriteInt32(PlayerContentTuningID);
            data.WriteInt32(TargetContentTuningID);
            data.WriteInt32(Unused927);
            data.WriteBits((uint)TuningType, 4);
            data.FlushBits();
        }

        public ContentTuningType TuningType;
        public short PlayerLevelDelta;
        public float PlayerItemLevel;
        public float TargetItemLevel;
        public uint ScalingHealthItemLevelCurveID;
        public byte TargetLevel;
        public byte Expansion;
        public sbyte TargetScalingLevelDelta;
        public ContentTuningFlags Flags = ContentTuningFlags.NoLevelScaling | ContentTuningFlags.NoItemLevelScaling;
        public int PlayerContentTuningID;
        public int TargetContentTuningID;
        public int Unused927;

        public enum ContentTuningType
        {
            CreatureToPlayerDamage = 1,
            PlayerToCreatureDamage = 2,
            CreatureToCreatureDamage = 4,
            PlayerToSandboxScaling = 7, // NYI
            PlayerToPlayerExpectedStat = 8
        }

        public enum ContentTuningFlags
        {
            NoLevelScaling = 0x1,
            NoItemLevelScaling = 0x2
        }
    }

    struct CombatWorldTextViewerInfo
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(ViewerGUID);
            data.WriteBit(ColorType.HasValue);
            data.WriteBit(ScaleType.HasValue);
            data.FlushBits();

            if (ColorType.HasValue)
                data.WriteUInt8(ColorType.Value);

            if (ScaleType.HasValue)
                data.WriteUInt8(ScaleType.Value);
        }
        
        public ObjectGuid ViewerGUID;
        public byte? ColorType;
        public byte? ScaleType;
    }

    public struct SpellSupportInfo
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(CasterGUID);
            data.WriteInt32(SpellID);
            data.WriteInt32(Amount);
            data.WriteFloat(Percentage);
        }
        
        public ObjectGuid CasterGUID;
        public int SpellID;
        public int Amount;
        public float Percentage;
    }

    public struct SpellCastVisual
    {
        public SpellCastVisual(int spellXSpellVisualID/*, int scriptVisualID*/)
        {
            SpellXSpellVisualID = spellXSpellVisualID;
            //ScriptVisualID = scriptVisualID;
        }

        public void Read(WorldPacket data)
        {
            SpellXSpellVisualID = data.ReadInt32();
            //ScriptVisualID = data.ReadInt32();
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(SpellXSpellVisualID);
            //data.WriteInt32(ScriptVisualID);
        }
        
        public int SpellXSpellVisualID;
        //public int ScriptVisualID;
    }

    public class AuraDataInfo
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(CastID);
            data.WriteInt32(SpellID);

            Visual.Write(data);

            data.WriteUInt16((ushort)Flags);
            data.WriteUInt32(ActiveFlags);
            data.WriteUInt16(CastLevel);
            data.WriteUInt8(Applications);
            data.WriteInt32(ContentTuningID);
            data.WriteBit(CastUnit.HasValue);
            data.WriteBit(Duration.HasValue);
            data.WriteBit(Remaining.HasValue);
            data.WriteBit(TimeMod.HasValue);
            data.WriteBits(Points.Count, 6);
            data.WriteBits(EstimatedPoints.Count, 6);
            data.WriteBit(ContentTuning != null);

            if (ContentTuning != null)
                ContentTuning.Write(data);

            if (CastUnit.HasValue)
                data.WritePackedGuid(CastUnit.Value);

            if (Duration.HasValue)
                data.WriteInt32(Duration.Value);

            if (Remaining.HasValue)
                data.WriteInt32(Remaining.Value);

            if (TimeMod.HasValue)
                data.WriteFloat(TimeMod.Value);

            foreach (var point in Points)
                data.WriteFloat(point);

            foreach (var point in EstimatedPoints)
                data.WriteFloat(point);
        }

        public ObjectGuid CastID;
        public int SpellID;
        public SpellCastVisual Visual;
        public AuraFlags Flags;
        public uint ActiveFlags;
        public ushort CastLevel = 1;
        public byte Applications = 1;
        public int ContentTuningID;
        ContentTuningParams ContentTuning;
        public ObjectGuid? CastUnit;
        public int? Duration;
        public int? Remaining;
        float? TimeMod;
        public List<float> Points = new();
        public List<float> EstimatedPoints = new();
    }

    public struct AuraInfo
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8(Slot);
            data.WriteBit(AuraData != null);
            data.FlushBits();

            if (AuraData != null)
                AuraData.Write(data);
        }

        public byte Slot;
        public AuraDataInfo AuraData;
    }

    public class TargetLocation
    {
        public void Read(WorldPacket data)
        {
            Transport = data.ReadPackedGuid();
            Location = data.ReadVector3();
        }

        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(Transport);
            data.WriteVector3(Location);
        }
        
        public ObjectGuid Transport;
        public Vector3 Location;
    }

    public class SpellTargetData
    {
        public void Read(WorldPacket data)
        {
            data.ResetBitPos();
            Flags = (SpellCastTargetFlags)data.ReadBits<uint>(28);
            if (data.HasBit())
                SrcLocation = new();

            if (data.HasBit())
                DstLocation = new();

            bool hasOrientation = data.HasBit();
            bool hasMapId = data.HasBit();

            int nameLength = data.ReadBits<int>(7);

            Unit = data.ReadPackedGuid();
            Item = data.ReadPackedGuid();

            if (SrcLocation != null)
                SrcLocation.Read(data);

            if (DstLocation != null)
                DstLocation.Read(data);

            if (hasOrientation)
                Orientation = data.ReadFloat();

            if (hasMapId)
                MapID = data.ReadInt32();

            Name = data.ReadString(nameLength);
        }

        public void Write(WorldPacket data)
        {
            data.WriteBits((uint)Flags, 28);
            data.WriteBit(SrcLocation != null);
            data.WriteBit(DstLocation != null);
            data.WriteBit(Orientation.HasValue);
            data.WriteBit(MapID.HasValue);
            data.WriteBits(Name.GetByteCount(), 7);
            data.FlushBits();

            data.WritePackedGuid(Unit);
            data.WritePackedGuid(Item);

            if (SrcLocation != null)
                SrcLocation.Write(data);

            if (DstLocation != null)
                DstLocation.Write(data);

            if (Orientation.HasValue)
                data.WriteFloat(Orientation.Value);

            if (MapID.HasValue)
                data.WriteInt32(MapID.Value);

            data.WriteString(Name);
        }

        public SpellCastTargetFlags Flags;
        public ObjectGuid Unit;
        public ObjectGuid Item;
        public TargetLocation SrcLocation;
        public TargetLocation DstLocation;
        public float? Orientation;
        public int? MapID;
        public string Name = string.Empty;
    }

    public struct MissileTrajectoryRequest
    {
        public void Read(WorldPacket data)
        {
            Pitch = data.ReadFloat();
            Speed = (Speed)data.ReadFloat();
        }
        
        public float Pitch;
        public Speed Speed;
    }

    public struct SpellWeight
    {
        public uint Type;
        public int ID;
        public uint Quantity;
    }

    public struct SpellCraftingReagent
    {
        public void Read(WorldPacket data)
        {
            ItemID = data.ReadInt32();
            DataSlotIndex = data.ReadInt32();
            Quantity = data.ReadInt32();
            if (data.HasBit())
                Unknown_1000 = data.ReadUInt8();
        }
        
        public int ItemID;
        public int DataSlotIndex;
        public int Quantity;
        public byte? Unknown_1000;
    }

    public struct SpellExtraCurrencyCost
    {
        public void Read(WorldPacket data)
        {
            CurrencyID = data.ReadInt32();
            Count = data.ReadInt32();
        }
        
        public int CurrencyID;
        public int Count;
    }

    public class SpellCastRequestPkt
    {   
        public void Read(WorldPacket data)
        {
            CastID = data.ReadPackedGuid();
            Misc[0] = data.ReadInt32();
            Misc[1] = data.ReadInt32();
            SpellID = data.ReadInt32();
            Visual.Read(data);
            MissileTrajectory.Read(data);
            CraftingNPC = data.ReadPackedGuid();

            var optionalCurrenciesCount = data.ReadUInt32();
            var optionalReagentsCount = data.ReadUInt32();
            var removedModificationsCount = data.ReadUInt32();

            for (var i = 0; i < optionalCurrenciesCount; ++i)
                OptionalCurrencies[i].Read(data);

            SendCastFlags = (byte)data.ReadBits<uint>(5);
            bool hasMoveUpdate = data.HasBit();
            var weightCount = data.ReadBits<uint>(2);
            bool hasCraftingOrderID = data.HasBit();
            Target.Read(data);

            if (hasCraftingOrderID)
                CraftingOrderID = data.ReadUInt64();

            for (var i = 0; i < optionalReagentsCount; ++i)
                OptionalReagents[i].Read(data);

            for (var i = 0; i < removedModificationsCount; ++i)
                RemovedModifications[i].Read(data);

            if (hasMoveUpdate)
                MoveUpdate.Read(data);

            for (var i = 0; i < weightCount; ++i)
            {
                data.ResetBitPos();
                SpellWeight weight;
                weight.Type = data.ReadBits<uint>(2);
                weight.ID = data.ReadInt32();
                weight.Quantity = data.ReadUInt32();
                Weight.Add(weight);
            }
        }

        public ObjectGuid CastID;
        public int SpellID;
        public SpellCastVisual Visual;
        public byte SendCastFlags;
        public SpellTargetData Target = new();
        public MissileTrajectoryRequest MissileTrajectory;
        public MovementInfo MoveUpdate = new();
        public List<SpellWeight> Weight = new();
        public Array<SpellCraftingReagent> OptionalReagents = new(6);
        public Array<SpellCraftingReagent> RemovedModifications = new(6);
        public Array<SpellExtraCurrencyCost> OptionalCurrencies = new(5 /*MAX_ITEM_EXT_COST_CURRENCIES*/);
        public ulong? CraftingOrderID;
        public ObjectGuid CraftingNPC;
        public int[] Misc = new int[2];
    }

    public struct SpellMissStatus
    {
        public SpellMissStatus(SpellMissInfo reason, SpellMissInfo reflectStatus)
        {
            Reason = reason;
            ReflectStatus = reflectStatus;
        }

        public void Write(WorldPacket data)
        {
            data.WriteUInt8((byte)Reason);
            if (Reason == SpellMissInfo.Reflect)
                data.WriteUInt8((byte)ReflectStatus);
        }

        public SpellMissInfo Reason;
        public SpellMissInfo ReflectStatus;
    }

    public struct SpellPowerData
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(Cost);
            data.WriteInt8((sbyte)Type);
        }
        
        public int Cost;
        public PowerType Type;
    }

    public class RuneData
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8(Start);
            data.WriteUInt8(Count);
            data.WriteInt32(Cooldowns.Count);

            foreach (byte cd in Cooldowns)
                data.WriteUInt8(cd);
        }

        public byte Start;
        public byte Count;
        public List<byte> Cooldowns = new();
    }

    public struct MissileTrajectoryResult
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32((Milliseconds)TravelTime);
            data.WriteFloat(Pitch);
        }
        
        public TimeSpan TravelTime;
        public float Pitch;
    }

    public struct CreatureImmunities
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt32(School);
            data.WriteUInt32(Value);
        }
        
        public uint School;
        public uint Value;
    }

    public struct SpellHealPrediction
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt32(Points);
            data.WriteUInt8((byte)Type);
            data.WritePackedGuid(BeaconGUID);
        }
        
        public ObjectGuid BeaconGUID;
        public uint Points;
        public SpellHealPredictionType Type;
    }

    public class SpellCastData
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(CasterGUID);
            data.WritePackedGuid(CasterUnit);
            data.WritePackedGuid(CastID);
            data.WritePackedGuid(OriginalCastID);
            data.WriteInt32(SpellID);
            Visual.Write(data);
            data.WriteUInt32((uint)CastFlags);
            data.WriteUInt32((uint)CastFlagsEx);
            data.WriteUInt32(CastTime);
            MissileTrajectory.Write(data);
            data.WriteUInt8(DestLocSpellCastIndex);
            Immunities.Write(data);
            Predict.Write(data);

            data.WriteBits(HitTargets.Count, 16);
            data.WriteBits(MissTargets.Count, 16);
            data.WriteBits(MissStatus.Count, 16);
            data.WriteBits(RemainingPower.Count, 9);
            data.WriteBit(RemainingRunes != null);
            data.WriteBits(TargetPoints.Count, 16);
            data.WriteBit(AmmoDisplayID.HasValue);
            data.WriteBit(AmmoInventoryType.HasValue);
            data.FlushBits();

            Target.Write(data);

            foreach (ObjectGuid hitTarget in HitTargets)
                data.WritePackedGuid(hitTarget);

            foreach (ObjectGuid missTarget in MissTargets)
                data.WritePackedGuid(missTarget);

            foreach (SpellMissStatus missStatus in MissStatus)
                missStatus.Write(data);

            foreach (SpellPowerData power in RemainingPower)
                power.Write(data);

            if (RemainingRunes != null)
                RemainingRunes.Write(data);

            foreach (TargetLocation targetLoc in TargetPoints)
                targetLoc.Write(data);

            if (AmmoDisplayID.HasValue)
                data.WriteInt32(AmmoDisplayID.Value);

            if (AmmoInventoryType.HasValue)
                data.WriteInt32(AmmoInventoryType.Value);
        }

        public ObjectGuid CasterGUID;
        public ObjectGuid CasterUnit;
        public ObjectGuid CastID;
        public ObjectGuid OriginalCastID;
        public int SpellID;
        public SpellCastVisual Visual;
        public SpellCastFlags CastFlags;
        public SpellCastFlagsEx CastFlagsEx;
        public RelativeTime CastTime;
        public List<ObjectGuid> HitTargets = new();
        public List<ObjectGuid> MissTargets = new();
        public List<SpellMissStatus> MissStatus = new();
        public SpellTargetData Target = new();
        public List<SpellPowerData> RemainingPower = new();
        public RuneData RemainingRunes;
        public MissileTrajectoryResult MissileTrajectory;
        public int? AmmoDisplayID;
        public int? AmmoInventoryType;
        public byte DestLocSpellCastIndex;
        public List<TargetLocation> TargetPoints = new();
        public CreatureImmunities Immunities;
        public SpellHealPrediction Predict;
    }

    public struct LearnedSpellInfo
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(SpellID);
            data.WriteBit(IsFavorite);
            data.WriteBit(field_8.HasValue);
            data.WriteBit(Superceded.HasValue);
            data.WriteBit(TraitDefinitionID.HasValue);
            data.FlushBits();

            if (field_8.HasValue)
                data.WriteInt32(field_8.Value);

            if (Superceded.HasValue)
                data.WriteInt32(Superceded.Value);

            if (TraitDefinitionID.HasValue)
                data.WriteInt32(TraitDefinitionID.Value);
        }
        
        public int SpellID;
        public bool IsFavorite;
        public int? field_8;
        public int? Superceded;
        public int? TraitDefinitionID;
    }

    public struct SpellModifierData
    {
        public void Write(WorldPacket data)
        {
            data.WriteFloat(ModifierValue);
            data.WriteUInt8(ClassIndex);
        }
        
        public float ModifierValue;
        public byte ClassIndex;
    }

    public class SpellModifierInfo
    {
        public void Write(WorldPacket data)
        {
            data.WriteUInt8(ModIndex);
            data.WriteInt32(ModifierData.Count);
            foreach (SpellModifierData modData in ModifierData)
                modData.Write(data);
        }
        
        public byte ModIndex;
        public List<SpellModifierData> ModifierData = new();
    }

    public class SpellCooldownStruct
    {
        public SpellCooldownStruct(int spellId, Milliseconds forcedCooldown)
        {
            SrecID = spellId;
            ForcedCooldown = forcedCooldown;
        }

        public void Write(WorldPacket data)
        {
            data.WriteInt32(SrecID);
            data.WriteInt32(ForcedCooldown);
            data.WriteFloat(ModRate);
        }

        public int SrecID;
        public Milliseconds ForcedCooldown;
        public float ModRate = 1.0f;
    }

    public class SpellHistoryEntry
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(SpellID);
            data.WriteInt32(ItemID);
            data.WriteInt32((int)Category);
            data.WriteInt32(RecoveryTime);
            data.WriteInt32(CategoryRecoveryTime);
            data.WriteFloat(ModRate);
            data.WriteBit(unused622_1.HasValue);
            data.WriteBit(unused622_2.HasValue);
            data.WriteBit(OnHold);
            data.FlushBits();

            if (unused622_1.HasValue)
                data.WriteUInt32(unused622_1.Value);
            if (unused622_2.HasValue)
                data.WriteUInt32(unused622_2.Value);
        }

        public int SpellID;
        public int ItemID;
        public SpellCategories Category;
        public int RecoveryTime;
        public int CategoryRecoveryTime;
        public float ModRate = 1.0f;
        public bool OnHold;
        uint? unused622_1;   // This field is not used for anything in the client in 6.2.2.20444
        uint? unused622_2;   // This field is not used for anything in the client in 6.2.2.20444
    }

    public class SpellChargeEntry
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32((int)Category);
            data.WriteUInt32(NextRecoveryTime);
            data.WriteFloat(ChargeModRate);
            data.WriteUInt8(ConsumedCharges);
        }

        public SpellCategories Category;
        public uint NextRecoveryTime;
        public float ChargeModRate = 1.0f;
        public byte ConsumedCharges;
    }

    public struct SpellChannelStartInterruptImmunities
    {
        public void Write(WorldPacket data)
        {
            data.WriteInt32(SchoolImmunities);
            data.WriteInt32(Immunities);
        }

        public int SchoolImmunities;
        public int Immunities;
    }

    public struct SpellTargetedHealPrediction
    {
        public void Write(WorldPacket data)
        {
            data.WritePackedGuid(TargetGUID);
            Predict.Write(data);
        }

        public ObjectGuid TargetGUID;
        public SpellHealPrediction Predict;
    }
}
