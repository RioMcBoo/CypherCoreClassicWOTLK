// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Scripting;

namespace Scripts.EasternKingdoms.Gnomeregan
{
    struct GNOGameObjectIds
    {
        public const int CaveInLeft = 146085;
        public const int CaveInRight = 146086;
        public const int RedRocket = 103820;
    }

    struct GNOCreatureIds
    {
        public const int BlastmasterEmiShortfuse = 7998;
        public const int CaverndeepAmbusher = 6207;
        public const int Grubbis = 7361;
        public const int ViciousFallout = 7079;
        public const int Chomper = 6215;
        public const int Electrocutioner = 6235;
        public const int CrowdPummeler = 6229;
        public const int Mekgineer = 7800;
    }

    struct DataTypes
    {
        public const int BlastmasterEvent = 0;
        public const int ViciousFallout = 1;
        public const int Electrocutioner = 2;
        public const int CrowdPummeler = 3;
        public const int Thermaplugg = 4;

        public const int MaxEncounter = 5;

        // Additional Objects
        public const int GoCaveInLeft = 6;
        public const int GoCaveInRight = 7;
        public const int NpcBastmasterEmiShortfuse = 8;
    }

    struct DataTypes64
    {
        public const int GoCaveInLeft = 0;
        public const int GoCaveInRight = 1;
        public const int NpcBastmasterEmiShortfuse = 2;
    }

    class instance_gnomeregan : InstanceMapScript
    {
        public instance_gnomeregan() : base(nameof(instance_gnomeregan), 90) { }

        class instance_gnomeregan_InstanceMapScript : InstanceScript
        {
            ObjectGuid uiCaveInLeftGUID;
            ObjectGuid uiCaveInRightGUID;

            ObjectGuid uiBlastmasterEmiShortfuseGUID;

            public instance_gnomeregan_InstanceMapScript(InstanceMap map) : base(map)
            {
                SetHeaders("GNO");
                SetBossNumber(DataTypes.MaxEncounter);
            }

            public override void OnCreatureCreate(Creature creature)
            {
                switch (creature.GetEntry())
                {
                    case GNOCreatureIds.BlastmasterEmiShortfuse:
                        uiBlastmasterEmiShortfuseGUID = creature.GetGUID();
                        break;
                }
            }

            public override void OnGameObjectCreate(GameObject go)
            {
                switch (go.GetEntry())
                {
                    case DataTypes64.GoCaveInLeft:
                        uiCaveInLeftGUID = go.GetGUID();
                        break;
                    case DataTypes64.GoCaveInRight:
                        uiCaveInRightGUID = go.GetGUID();
                        break;
                }
            }

            public override void OnUnitDeath(Unit unit)
            {
                Creature creature = unit.ToCreature();
                if (creature != null)
                    switch (creature.GetEntry())
                    {
                        case GNOCreatureIds.ViciousFallout:
                            SetBossState(DataTypes.ViciousFallout, EncounterState.Done);
                            break;
                        case GNOCreatureIds.Electrocutioner:
                            SetBossState(DataTypes.Electrocutioner, EncounterState.Done);
                            break;
                        case GNOCreatureIds.CrowdPummeler:
                            SetBossState(DataTypes.CrowdPummeler, EncounterState.Done);
                            break;
                        case GNOCreatureIds.Mekgineer:
                            SetBossState(DataTypes.Thermaplugg, EncounterState.Done);
                            break;
                    }
            }

            public override ObjectGuid GetGuidData(int uiType)
            {
                switch (uiType)
                {
                    case DataTypes64.GoCaveInLeft: return uiCaveInLeftGUID;
                    case DataTypes64.GoCaveInRight: return uiCaveInRightGUID;
                    case DataTypes64.NpcBastmasterEmiShortfuse: return uiBlastmasterEmiShortfuseGUID;
                }

                return ObjectGuid.Empty;
            }
        }

        public override InstanceScript GetInstanceScript(InstanceMap map)
        {
            return new instance_gnomeregan_InstanceMapScript(map);
        }
    }
}

