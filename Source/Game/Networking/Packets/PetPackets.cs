﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Networking.Packets
{
    class DismissCritter : ClientPacket
    {
        public DismissCritter(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            CritterGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid CritterGUID;
    }

    class RequestPetInfo : ClientPacket
    {
        public RequestPetInfo(WorldPacket packet) : base(packet) { }

        public override void Read() { }
    }

    class PetAbandon : ClientPacket
    {
        public PetAbandon(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Pet = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid Pet;
    }

    class PetStopAttack : ClientPacket
    {
        public PetStopAttack(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();
        }

        public ObjectGuid PetGUID;
    }

    class PetSpellAutocast : ClientPacket
    {
        public PetSpellAutocast(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();
            SpellID = _worldPacket.ReadInt32();
            AutocastEnabled = _worldPacket.HasBit();
        }

        public ObjectGuid PetGUID;
        public int SpellID;
        public bool AutocastEnabled;
    }

    public class PetSpells : ServerPacket
    {
        public PetSpells() : base(ServerOpcodes.PetSpellsMessage, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(PetGUID);
            _worldPacket.WriteUInt16((ushort)CreatureFamily);
            _worldPacket.WriteUInt16((ushort)Specialization);
            _worldPacket.WriteInt32(TimeLimit);
            _worldPacket.WriteUInt16((ushort)((byte)CommandState | (Flag << 16)));
            _worldPacket.WriteUInt8((byte)ReactState);

            foreach (var actionButton in ActionButtons)
                _worldPacket.WriteUInt32(actionButton.PackedData);

            _worldPacket.WriteInt32(Actions.Count);
            _worldPacket.WriteInt32(Cooldowns.Count);
            _worldPacket.WriteInt32(SpellHistory.Count);

            foreach (var action in Actions)
                _worldPacket.WriteUInt32(action.PackedData);

            foreach (PetSpellCooldown cooldown in Cooldowns)
            {
                _worldPacket.WriteInt32(cooldown.SpellID);
                _worldPacket.WriteUInt32(cooldown.Duration);
                _worldPacket.WriteUInt32(cooldown.CategoryDuration);
                _worldPacket.WriteFloat(cooldown.ModRate);
                _worldPacket.WriteUInt16(cooldown.Category);
            }

            foreach (PetSpellHistory history in SpellHistory)
            {
                _worldPacket.WriteInt32((int)history.CategoryID);
                _worldPacket.WriteUInt32(history.RecoveryTime);
                _worldPacket.WriteFloat(history.ChargeModRate);
                _worldPacket.WriteInt8(history.ConsumedCharges);
            }
        }

        public ObjectGuid PetGUID;
        public CreatureFamily CreatureFamily;
        public ChrSpecialization Specialization;
        public Milliseconds TimeLimit;
        public ReactStates ReactState;
        public CommandStates CommandState;
        public byte Flag;

        public CharmActionButton[] ActionButtons = new CharmActionButton[10];

        public List<CharmActionButton> Actions = new();
        public List<PetSpellCooldown> Cooldowns = new();
        public List<PetSpellHistory> SpellHistory = new();
    }

    class PetStableResult : ServerPacket
    {
        public PetStableResult() : base(ServerOpcodes.PetStableResult, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteUInt8((byte)Result);
        }

        public StableResult Result;
    }

    class PetLearnedSpells : ServerPacket
    {
        public PetLearnedSpells() : base(ServerOpcodes.PetLearnedSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Spells.Count);
            foreach (uint spell in Spells)
                _worldPacket.WriteUInt32(spell);
        }

        public List<int> Spells = new();
    }

    class PetUnlearnedSpells : ServerPacket
    {
        public PetUnlearnedSpells() : base(ServerOpcodes.PetUnlearnedSpells, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(Spells.Count);
            foreach (int spell in Spells)
                _worldPacket.WriteInt32(spell);
        }

        public List<int> Spells = new();
    }

    class PetNameInvalid : ServerPacket
    {
        public PetNameInvalid() : base(ServerOpcodes.PetNameInvalid) { }

        public override void Write()
        {
            _worldPacket.WriteUInt8((byte)Result);
            _worldPacket.WritePackedGuid(RenameData.PetGUID);
            _worldPacket.WriteInt32(RenameData.PetNumber);

            _worldPacket.WriteUInt8((byte)RenameData.NewName.GetByteCount());

            _worldPacket.WriteBit(RenameData.HasDeclinedNames);

            if (RenameData.HasDeclinedNames)
            {
                for (int i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                    _worldPacket.WriteBits(RenameData.DeclinedNames.Name[i].GetByteCount(), 7);

                for (int i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                    _worldPacket.WriteString(RenameData.DeclinedNames.Name[i]);
            }

            _worldPacket.WriteString(RenameData.NewName);
        }

        public PetRenameData RenameData;
        public PetNameInvalidReason Result;
    }

    class PetRename : ClientPacket
    {
        public PetRename(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            RenameData.PetGUID = _worldPacket.ReadPackedGuid();
            RenameData.PetNumber = _worldPacket.ReadInt32();

            int nameLen = _worldPacket.ReadBits<int>(8);

            RenameData.HasDeclinedNames = _worldPacket.HasBit();
            if (RenameData.HasDeclinedNames)
            {
                RenameData.DeclinedNames = new DeclinedName();
                int[] count = new int[SharedConst.MaxDeclinedNameCases];
                for (int i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                    count[i] = _worldPacket.ReadBits<int>(7);

                for (int i = 0; i < SharedConst.MaxDeclinedNameCases; i++)
                    RenameData.DeclinedNames.Name[i] = _worldPacket.ReadString(count[i]);
            }

            RenameData.NewName = _worldPacket.ReadString(nameLen);
        }

        public PetRenameData RenameData;
    }

    class PetAction : ClientPacket
    {
        public PetAction(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();

            Button = new(_worldPacket.ReadUInt32());
            TargetGUID = _worldPacket.ReadPackedGuid();

            ActionPosition = _worldPacket.ReadVector3();
        }

        public ObjectGuid PetGUID;
        public CharmActionButton Button;
        public ObjectGuid TargetGUID;
        public Vector3 ActionPosition;
    }

    class PetSetAction : ClientPacket
    {
        public PetSetAction(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            PetGUID = _worldPacket.ReadPackedGuid();

            Index = _worldPacket.ReadInt32();
            ActionButton = new(_worldPacket.ReadUInt32());
        }

        public ObjectGuid PetGUID;
        public int Index;
        public CharmActionButton ActionButton;
    }

    class SetPetSpecialization : ServerPacket
    {
        public SetPetSpecialization() : base(ServerOpcodes.SetPetSpecialization) { }

        public override void Write()
        {
            _worldPacket.WriteUInt16(SpecID);
        }

        public ushort SpecID;
    }

    class PetActionFeedbackPacket : ServerPacket
    {
        public PetActionFeedbackPacket() : base(ServerOpcodes.PetStableResult) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SpellID);
            _worldPacket.WriteUInt8((byte)Response);
        }

        public int SpellID;
        public PetActionFeedback Response;
    }

    class PetActionSound : ServerPacket
    {
        public PetActionSound() : base(ServerOpcodes.PetStableResult) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(UnitGUID);
            _worldPacket.WriteInt32((int)Action);
        }

        public ObjectGuid UnitGUID;
        public PetTalk Action;
    }

    class PetTameFailure : ServerPacket
    {
        public PetTameFailure() : base(ServerOpcodes.PetTameFailure) { }

        public override void Write()
        {
            _worldPacket.WriteUInt8(Result);
        }

        public byte Result;
    }

    class PetMode : ServerPacket
    {
        public PetMode() : base(ServerOpcodes.PetMode, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(PetGUID);
            _worldPacket.WriteUInt16((ushort)((int)CommandState | Flag << 8));
            _worldPacket.WriteUInt8((byte)ReactState);
        }
        
        public ObjectGuid PetGUID;
        public ReactStates ReactState;
        public CommandStates CommandState;
        public byte Flag;
    }

    //Structs
    public class PetSpellCooldown
    {
        public int SpellID;
        public uint Duration;
        public uint CategoryDuration;
        public float ModRate = 1.0f;
        public ushort Category;
    }

    public class PetSpellHistory
    {
        public SpellCategories CategoryID;
        public uint RecoveryTime;
        public float ChargeModRate = 1.0f;
        public sbyte ConsumedCharges;
    }

    struct PetRenameData
    {
        public ObjectGuid PetGUID;
        public int PetNumber;
        public string NewName;
        public bool HasDeclinedNames;
        public DeclinedName DeclinedNames;
    }
}
