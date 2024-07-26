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
            foreach (var wsi in Worldstates)
            {
                _worldPacket.WriteInt32(wsi.state);
                _worldPacket.WriteInt32(wsi.value);
            }
        }

        public void AddState(int worldStateId, WorldStateValue value)
        {
            Worldstates.Add((worldStateId, value));
        }

        public int AreaID;
        public int SubareaID;
        public int MapID;

        List<(int state, WorldStateValue value)> Worldstates = new();
        
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

        public WorldStateValue Value;
        public bool Hidden; // @todo: research
        public int VariableID;
    }    
}
