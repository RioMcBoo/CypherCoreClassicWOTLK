// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Game.DataStorage
{
    public sealed class JournalEncounterRecord
    {
        public int Id;
        public LocalizedString Name;
        public LocalizedString Description;
        public Vector2 Map;
        public ushort JournalInstanceID;
        public uint OrderIndex;
        public ushort FirstSectionID;
        public ushort UiMapID;
        public uint MapDisplayConditionID;
        public int Flags;
        public sbyte DifficultyMask;
    }

    public sealed class JournalEncounterSectionRecord
    {
        public int Id;
        public LocalizedString Title;
        public LocalizedString BodyText;
        public ushort JournalEncounterID;
        public byte OrderIndex;
        public ushort ParentSectionID;
        public ushort FirstChildSectionID;
        public ushort NextSiblingSectionID;
        public byte Type;
        public uint IconCreatureDisplayInfoID;
        public int UiModelSceneID;
        public int SpellID;
        public int IconFileDataID;
        public int Flags;
        public int IconFlags;
        public sbyte DifficultyMask;
    }

    public sealed class JournalInstanceRecord
    {        
        public LocalizedString Name;
        public LocalizedString Description;
        public int Id;
        public ushort MapID;
        public int BackgroundFileDataID;
        public int ButtonFileDataID;
        public int ButtonSmallFileDataID;
        public int LoreFileDataID;
        public int Flags;
        public ushort AreaID;
    }

    public sealed class JournalTierRecord
    {
        public int Id;
        public LocalizedString Name;
    }
}
