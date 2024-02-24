// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

namespace Game.Networking.Packets
{
    class CheckIsAdventureMapPoiValid : ClientPacket
    {
        public CheckIsAdventureMapPoiValid(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            AdventureMapPoiID = _worldPacket.ReadInt32();
        }

        public int AdventureMapPoiID;
    }

    class PlayerIsAdventureMapPoiValid : ServerPacket
    {
        public PlayerIsAdventureMapPoiValid() : base(ServerOpcodes.PlayerIsAdventureMapPoiValid, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(AdventureMapPoiID);
            _worldPacket.WriteBit(IsVisible);
            _worldPacket.FlushBits();
        }

        public int AdventureMapPoiID;
        public bool IsVisible;
    }

    class AdventureMapStartQuest : ClientPacket
    {
        public AdventureMapStartQuest(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            QuestID = _worldPacket.ReadInt32();
        }

        public int QuestID;
    }
}
