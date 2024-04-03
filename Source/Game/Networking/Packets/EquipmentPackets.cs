// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class EquipmentSetID : ServerPacket
    {
        public EquipmentSetID() : base(ServerOpcodes.EquipmentSetId, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt64(GUID);
            _worldPacket.WriteInt32(Type);
            _worldPacket.WriteInt32(SetID);
        }

        public long GUID; // Set Identifier
        public int Type;
        public int SetID; // Index
    }

    public class LoadEquipmentSet : ServerPacket
    {
        public LoadEquipmentSet() : base(ServerOpcodes.LoadEquipmentSet, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SetData.Count);

            foreach (var equipSet in SetData)
                equipSet.Write(_worldPacket);
        }

        public List<EquipmentSetInfo.EquipmentSetData> SetData = new();
    }

    public class SaveEquipmentSet : ClientPacket
    {
        public SaveEquipmentSet(WorldPacket packet) : base(packet)
        {
            Set = new EquipmentSetInfo.EquipmentSetData();
        }

        public override void Read()
        {
            Set.Read(_worldPacket);
        }

        public EquipmentSetInfo.EquipmentSetData Set;
    }

    class DeleteEquipmentSet : ClientPacket
    {
        public DeleteEquipmentSet(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            ID = _worldPacket.ReadInt64();
        }

        public long ID;
    }

    class UseEquipmentSet : ClientPacket
    {
        public UseEquipmentSet(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            Inv = new InvUpdate(_worldPacket);

            for (byte i = 0; i < EquipmentSlot.End; ++i)
            {
                Items[i].Item = _worldPacket.ReadPackedGuid();
                Items[i].ContainerSlot = _worldPacket.ReadUInt8();
                Items[i].Slot = _worldPacket.ReadUInt8();
            }

            GUID = _worldPacket.ReadUInt64();
        }

        public InvUpdate Inv;
        public EquipmentSetItem[] Items = new EquipmentSetItem[EquipmentSlot.End];
        public ulong GUID; //Set Identifier

        public struct EquipmentSetItem
        {
            public ObjectGuid Item;
            public byte ContainerSlot;
            public byte Slot;
        }
    }

    class UseEquipmentSetResult : ServerPacket
    {
        public UseEquipmentSetResult() : base(ServerOpcodes.UseEquipmentSetResult) { }

        public override void Write()
        {
            _worldPacket.WriteUInt64(GUID);
            _worldPacket.WriteUInt8(Reason);
        }

        public ulong GUID; //Set Identifier
        public byte Reason;
    }
}
