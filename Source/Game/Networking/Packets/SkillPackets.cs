// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System.Collections.Generic;

namespace Game.Networking.Packets
{
    class ShowTradeSkill : ClientPacket
    {
        public ShowTradeSkill(WorldPacket packet) : base(packet) { }

        public override void Read()
        {
            CasterGUID = _worldPacket.ReadPackedGuid();
            SpellId = _worldPacket.ReadInt32();
            SkillId = (SkillType)_worldPacket.ReadInt32();
        }

        public ObjectGuid CasterGUID;
        public int SpellId;
        public SkillType SkillId;
    }

    public class ShowTradeSkillResponse : ServerPacket
    {
        public ShowTradeSkillResponse() : base(ServerOpcodes.ShowTradeSkillResponse, ConnectionType.Instance) { }

        public override void Write()
        {
            _worldPacket.WritePackedGuid(CasterGUID);
            _worldPacket.WriteInt32(SpellId);

            _worldPacket.WriteInt32(SkillLineIDs.Count);
            _worldPacket.WriteInt32(SkillRanks.Count);
            _worldPacket.WriteInt32(SkillMaxRanks.Count);
            _worldPacket.WriteInt32(KnownAbilitySpellIDs.Count);

            foreach(var skillLineId in SkillLineIDs)
                _worldPacket.WriteInt32(skillLineId);

            foreach (var skillRank in SkillRanks)
                _worldPacket.WriteInt32(skillRank);

            foreach (var skillMaxRank in SkillMaxRanks)
                _worldPacket.WriteInt32(skillMaxRank);

            _worldPacket.WriteInt32((int)SkillLineId);
            _worldPacket.WriteInt32(SkillRank);
            _worldPacket.WriteInt32(SkillMaxRank);

            foreach (var knownId in KnownAbilitySpellIDs)
                _worldPacket.WriteInt32(knownId);
        }

        public ObjectGuid CasterGUID;
        public int SpellId;
        public SkillType SkillLineId;
        public int SkillRank;
        public int SkillMaxRank;
        private List<int> SkillLineIDs = [0];
        private List<int> SkillRanks = [0];
        private List<int> SkillMaxRanks = [0];
        public List<int> KnownAbilitySpellIDs;
    }
}
