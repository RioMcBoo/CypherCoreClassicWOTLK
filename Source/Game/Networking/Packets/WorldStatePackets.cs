// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    public class InitWorldStates : ServerPacket
    {
        public InitWorldStates() : base(ServerOpcodes.InitWorldStates, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(MapID);
            _worldPacket.WriteInt32(AreaID);
            _worldPacket.WriteInt32(SubareaID);

            _worldPacket.WriteInt32(Worldstates.Count);
            foreach (WorldStateInfo wsi in Worldstates)
            {
                _worldPacket.WriteInt32(wsi.VariableID);
                _worldPacket.WriteInt32(wsi.Value);
            }
        }

        public void AddState(WorldStates variableID, int value)
        {
            AddState(variableID, value);
        }

        public void AddState(int variableID, int value)
        {
            Worldstates.Add(new WorldStateInfo(variableID, value));
        }

        public void AddState(WorldStates variableID, bool value)
        {
            AddState(variableID, value);
        }

        public void AddState(int variableID, bool value)
        {
            Worldstates.Add(new WorldStateInfo(variableID, value ? 1 : 0));
        }

        public int AreaID;
        public int SubareaID;
        public int MapID;

        List<WorldStateInfo> Worldstates = new();

        struct WorldStateInfo
        {
            public WorldStateInfo(int variableID, int value)
            {
                VariableID = variableID;
                Value = value;
            }

            public int VariableID;
            public int Value;
        }
    }

    public class UpdateWorldState : ServerPacket
    {
        public UpdateWorldState() : base(ServerOpcodes.UpdateWorldState, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(VariableID);
            _worldPacket.WriteInt32(Value);
            _worldPacket.WriteBit(Hidden);
            _worldPacket.FlushBits();
        }

        public int Value;
        public bool Hidden; // @todo: research
        public int VariableID;
    }
}
