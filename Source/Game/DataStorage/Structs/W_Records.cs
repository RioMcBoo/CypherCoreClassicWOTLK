// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;

namespace Game.DataStorage
{
    public sealed class WMOAreaTableRecord
    {
        public LocalizedString AreaName;
        public int Id;
        /// <summary>
        /// used in root WMO
        /// </summary>
        public ushort WmoID;
        /// <summary>
        /// used in adt file
        /// </summary>
        public byte NameSetID;
        /// <summary>
        /// used in group WMO
        /// </summary>
        public int WmoGroupID;
        public byte SoundProviderPref;
        public byte SoundProviderPrefUnderwater;
        public ushort AmbienceID;
        public ushort UwAmbience;
        public ushort ZoneMusic;
        public int UwZoneMusic;
        public ushort IntroSound;
        public ushort UwIntroSound;
        public ushort AreaTableID;
        public byte Flags;
    }

    public sealed class WorldEffectRecord
    {
        public int Id;
        public int QuestFeedbackEffectID;
        public byte WhenToDisplay;
        public byte TargetType;
        public int TargetAsset;
        public int PlayerConditionID;
        public ushort CombatConditionID;
    }

    public sealed class WorldMapOverlayRecord
    {
        public int Id;
        public int UiMapArtID;
        public ushort TextureWidth;
        public ushort TextureHeight;
        public int OffsetX;
        public int OffsetY;
        public int HitRectTop;
        public int HitRectBottom;
        public int HitRectLeft;
        public int HitRectRight;
        public int PlayerConditionID;
        public uint Flags;
        public int[] AreaID = new int[SharedConst.MaxWorldMapOverlayArea];
    }

    public sealed class WorldStateExpressionRecord
    {
        public int Id;
        public string Expression;
    }
}
