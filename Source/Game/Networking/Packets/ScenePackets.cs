// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;

namespace Game.Networking.Packets
{
    class PlayScene : ServerPacket
    {
        public PlayScene() : base(ServerOpcodes.PlayScene, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SceneID);
            _worldPacket.WriteUInt32(PlaybackFlags);
            _worldPacket.WriteInt32(SceneInstanceID);
            _worldPacket.WriteInt32(SceneScriptPackageID);
            _worldPacket.WritePackedGuid(TransportGUID);
            _worldPacket.WriteVector4(Location.GetPosition4D());
            _worldPacket.WriteBit(Encrypted);
            _worldPacket.FlushBits();
        }

        public int SceneID;
        public uint PlaybackFlags;
        public int SceneInstanceID;
        public int SceneScriptPackageID;
        public ObjectGuid TransportGUID;
        public Position Location;
        public bool Encrypted;
    }

    class CancelScene : ServerPacket
    {
        public CancelScene() : base(ServerOpcodes.CancelScene, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WriteInt32(SceneInstanceID);
        }

        public int SceneInstanceID;
    }

    class SceneTriggerEvent : ClientPacket
    {
        public SceneTriggerEvent(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            int len = _worldPacket.ReadBits<int>(6);
            SceneInstanceID = _worldPacket.ReadInt32();
            _Event = _worldPacket.ReadString(len);
        }

        public int SceneInstanceID;
        public string _Event;
    }

    class ScenePlaybackComplete : ClientPacket
    {
        public ScenePlaybackComplete(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SceneInstanceID = _worldPacket.ReadInt32();
        }

        public int SceneInstanceID;
    }

    class ScenePlaybackCanceled : ClientPacket
    {
        public ScenePlaybackCanceled(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            SceneInstanceID = _worldPacket.ReadInt32();
        }

        public int SceneInstanceID;
    }
}
