// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using static Game.AI.SmartAction;

namespace Game.DataStorage
{
    public sealed class NameGenRecord
    {
        public int Id;
        public string Name;
        private byte _raceID;
        private byte _sex;

        #region Properties
        public Race RaceID => (Race)_raceID;
        public Gender Sex => (Gender)_sex;
        #endregion
    }

    public sealed class NamesProfanityRecord
    {
        public uint Id;
        public string Name;
        private sbyte _language;

        #region Properties
        public Locale Language => (Locale)_language;
        #endregion
    }

    public sealed class NamesReservedRecord
    {
        public uint Id;
        public string Name;
    }

    public sealed class NamesReservedLocaleRecord
    {
        public uint Id;
        public string Name;
        private byte _localeMask;

        #region Properties
        public LocaleMask LocaleMask => (LocaleMask)_localeMask;
        #endregion
    }

    public sealed class NumTalentsAtLevelRecord
    {
        public uint Id;
        public int NumTalents;
        public int NumTalentsDeathKnight;
        public int NumTalentsDemonHunter;
    }
}
