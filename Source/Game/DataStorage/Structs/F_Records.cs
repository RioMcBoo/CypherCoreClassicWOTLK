// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.DataStorage
{
    public sealed class FactionRecord
    {
        public long[] ReputationRaceMask = new long[4];
        public LocalizedString Name;
        public LocalizedString Description;
        public int Id;
        public short ReputationIndex;
        public ushort ParentFactionID;
        public byte Expansion;
        public byte FriendshipRepID;
        public int Flags;
        public ushort ParagonFactionID;
        public int RenownFactionID;
        public int RenownCurrencyID;
        private short[] _reputationClassMask = new short[4];
        public ushort[] ReputationFlags = new ushort[4];
        public int[] ReputationBase = new int[4];
        public short[] ReputationMax = new short[4];
        /// <summary>
        /// Faction outputs rep * ParentFactionModOut as spillover reputation
        /// </summary>
        public float[] ParentFactionMod = new float[2];
        /// <summary>
        /// The highest rank the faction will profit from incoming spillover
        /// </summary>
        public byte[] ParentFactionCap = new byte[2];

        #region Properties
        public ClassMask ReputationClassMask(int index) => (ClassMask)_reputationClassMask[index];
        #endregion

        #region Helpers
        public bool CanHaveReputation => ReputationIndex >= 0;
        #endregion
    }

    public sealed class FactionTemplateRecord
    {
        static int MAX_FACTION_RELATIONS = 8;

        public int Id;
        public ushort Faction;
        private ushort _flags;
        public byte FactionGroup;
        public byte FriendGroup;
        public byte EnemyGroup;
        public ushort[] Enemies = new ushort[MAX_FACTION_RELATIONS];
        public ushort[] Friend = new ushort[MAX_FACTION_RELATIONS];

       
        #region Properties
        public FactionTemplateFlags Flags => (FactionTemplateFlags)_flags;        
        #endregion

        #region Helpers
        public bool HasFlag(FactionTemplateFlags flag)
        {
            return _flags.HasFlag((ushort)flag);
        }

        public bool HasAnyFlag(FactionTemplateFlags flag)
        {
            return _flags.HasAnyFlag((ushort)flag);
        }

        public bool IsFriendlyTo(FactionTemplateRecord entry)
        {
            if (this == entry)
                return true;

            if (entry.Faction != 0)
            {
                for (int i = 0; i < MAX_FACTION_RELATIONS; ++i)
                    if (Enemies[i] == entry.Faction)
                        return false;
                for (int i = 0; i < MAX_FACTION_RELATIONS; ++i)
                    if (Friend[i] == entry.Faction)
                        return true;
            }
            return FriendGroup.HasAnyFlag(entry.FactionGroup) || FactionGroup.HasAnyFlag(entry.FriendGroup);
        }

        public bool IsHostileTo(FactionTemplateRecord entry)
        {
            if (this == entry)
                return false;

            if (entry.Faction != 0)
            {
                for (int i = 0; i < MAX_FACTION_RELATIONS; ++i)
                    if (Enemies[i] == entry.Faction)
                        return true;
                for (int i = 0; i < MAX_FACTION_RELATIONS; ++i)
                    if (Friend[i] == entry.Faction)
                        return false;
            }
            return EnemyGroup.HasAnyFlag(entry.FactionGroup);
        }
        public bool IsHostileToPlayers => EnemyGroup.HasFlag((byte)FactionMasks.Player);

        public bool IsNeutralToAll()
        {
            for (int i = 0; i < MAX_FACTION_RELATIONS; ++i)
                if (Enemies[i] != 0)
                    return false;
            return EnemyGroup == 0 && FriendGroup == 0;
        }
        public bool IsContestedGuardFaction => HasFlag(FactionTemplateFlags.ContestedGuard);
        #endregion
    }

    public sealed class FriendshipRepReactionRecord
    {
        public int Id;
        public LocalizedString Reaction;
        public byte FriendshipRepID;
        public ushort ReactionThreshold;
    }

    public sealed class FriendshipReputationRecord
    {
        public LocalizedString Description;
        public int Id;
        public int Field34146722002;
        public int Field34146722003;
    }
}