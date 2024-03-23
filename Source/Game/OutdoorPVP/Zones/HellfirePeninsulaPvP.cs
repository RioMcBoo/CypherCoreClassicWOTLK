// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using Game.Maps;
using Game.Networking.Packets;
using Game.Scripting;

namespace Game.PvP
{
    class HellfirePeninsulaPvP : OutdoorPvP
    {
        public HellfirePeninsulaPvP(Map map) : base(map)
        {
            m_TypeId = OutdoorPvPTypes.HellfirePeninsula;
            m_AllianceTowersControlled = 0;
            m_HordeTowersControlled = 0;
        }

        public override bool SetupOutdoorPvP()
        {
            m_AllianceTowersControlled = 0;
            m_HordeTowersControlled = 0;

            // add the zones affected by the pvp buff
            for (int i = 0; i < HPConst.BuffZones.Length; ++i)
                RegisterZone(HPConst.BuffZones[i]);

            return true;
        }

        public override void OnGameObjectCreate(GameObject go)
        {
            switch (go.GetEntry())
            {
                case 182175:
                    AddCapturePoint(new HellfirePeninsulaCapturePoint(this, OutdoorPvPHPTowerType.BrokenHill, go, m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.BrokenHill]));
                    break;
                case 182174:
                    AddCapturePoint(new HellfirePeninsulaCapturePoint(this, OutdoorPvPHPTowerType.Overlook, go, m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.Overlook]));
                    break;
                case 182173:
                    AddCapturePoint(new HellfirePeninsulaCapturePoint(this, OutdoorPvPHPTowerType.Stadium, go, m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.Stadium]));
                    break;
                case 183514:
                    m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.BrokenHill] = go.GetSpawnId();
                    break;
                case 182525:
                    m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.Overlook] = go.GetSpawnId();
                    break;
                case 183515:
                    m_towerFlagSpawnIds[(int)OutdoorPvPHPTowerType.Stadium] = go.GetSpawnId();
                    break;
                default:
                    break;
            }

            base.OnGameObjectCreate(go);
        }

        public override void HandlePlayerEnterZone(Player player, int zone)
        {
            // add buffs
            if (player.GetTeam() == Team.Alliance)
            {
                if (m_AllianceTowersControlled >= 3)
                    player.CastSpell(player, OutdoorPvPHPSpells.AllianceBuff, true);
            }
            else
            {
                if (m_HordeTowersControlled >= 3)
                    player.CastSpell(player, OutdoorPvPHPSpells.HordeBuff, true);
            }
            base.HandlePlayerEnterZone(player, zone);
        }

        public override void HandlePlayerLeaveZone(Player player, int zone)
        {
            // remove buffs
            if (player.GetTeam() == Team.Alliance)
                player.RemoveAurasDueToSpell(OutdoorPvPHPSpells.AllianceBuff);
            else
                player.RemoveAurasDueToSpell(OutdoorPvPHPSpells.HordeBuff);

            base.HandlePlayerLeaveZone(player, zone);
        }

        public override bool Update(uint diff)
        {
            bool changed = base.Update(diff);
            if (changed)
            {
                if (m_AllianceTowersControlled == 3)
                    TeamApplyBuff(TeamId.Alliance, OutdoorPvPHPSpells.AllianceBuff, OutdoorPvPHPSpells.HordeBuff);
                else if (m_HordeTowersControlled == 3)
                    TeamApplyBuff(TeamId.Horde, OutdoorPvPHPSpells.HordeBuff, OutdoorPvPHPSpells.AllianceBuff);
                else
                {
                    TeamCastSpell(TeamId.Alliance, -(int)OutdoorPvPHPSpells.AllianceBuff);
                    TeamCastSpell(TeamId.Horde, -(int)OutdoorPvPHPSpells.HordeBuff);
                }
                SetWorldState(OutdoorPvPHPWorldStates.Count_A, (int)m_AllianceTowersControlled);
                SetWorldState(OutdoorPvPHPWorldStates.Count_H, (int)m_HordeTowersControlled);
            }
            return changed;
        }

        public override void SendRemoveWorldStates(Player player)
        {
            InitWorldStates initWorldStates = new();
            initWorldStates.MapID = player.GetMapId();
            initWorldStates.AreaID = player.GetZoneId();
            initWorldStates.SubareaID = player.GetAreaId();
            initWorldStates.AddState(OutdoorPvPHPWorldStates.Display_A, 0);
            initWorldStates.AddState(OutdoorPvPHPWorldStates.Display_H, 0);
            initWorldStates.AddState(OutdoorPvPHPWorldStates.Count_H, 0);
            initWorldStates.AddState(OutdoorPvPHPWorldStates.Count_A, 0);

            for (int i = 0; i < (int)OutdoorPvPHPTowerType.Num; ++i)
            {
                initWorldStates.AddState(HPConst.Map_N[i], 0);
                initWorldStates.AddState(HPConst.Map_A[i], 0);
                initWorldStates.AddState(HPConst.Map_H[i], 0);
            }

            player.SendPacket(initWorldStates);
        }

        public override void HandleKillImpl(Player killer, Unit killed)
        {
            if (!killed.IsTypeId(TypeId.Player))
                return;

            if (killer.GetTeam() == Team.Alliance && killed.ToPlayer().GetTeam() != Team.Alliance)
                killer.CastSpell(killer, OutdoorPvPHPSpells.AlliancePlayerKillReward, true);
            else if (killer.GetTeam() == Team.Horde && killed.ToPlayer().GetTeam() != Team.Horde)
                killer.CastSpell(killer, OutdoorPvPHPSpells.HordePlayerKillReward, true);
        }

        public int GetAllianceTowersControlled()
        {
            return m_AllianceTowersControlled;
        }

        public void SetAllianceTowersControlled(int count)
        {
            m_AllianceTowersControlled = count;
        }

        public int GetHordeTowersControlled()
        {
            return m_HordeTowersControlled;
        }

        public void SetHordeTowersControlled(int count)
        {
            m_HordeTowersControlled = count;
        }

        // how many towers are controlled
        int m_AllianceTowersControlled;
        int m_HordeTowersControlled;
        long[] m_towerFlagSpawnIds = new long[(int)OutdoorPvPHPTowerType.Num];
    }

    class HellfirePeninsulaCapturePoint : OPvPCapturePoint
    {
        public HellfirePeninsulaCapturePoint(OutdoorPvP pvp, OutdoorPvPHPTowerType type, GameObject go, long flagSpawnId) : base(pvp)
        {
            m_TowerType = (int)type;
            m_flagSpawnId = flagSpawnId;

            m_capturePointSpawnId = go.GetSpawnId();
            m_capturePoint = go;
            SetCapturePointData(go.GetEntry());
        }

        public override void ChangeState()
        {
            int field = 0;
            switch (OldState)
            {
                case ObjectiveStates.Neutral:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.Alliance:
                    field = HPConst.Map_A[m_TowerType];
                    int alliance_towers = ((HellfirePeninsulaPvP)PvP).GetAllianceTowersControlled();
                    if (alliance_towers != 0)
                        ((HellfirePeninsulaPvP)PvP).SetAllianceTowersControlled(--alliance_towers);
                    break;
                case ObjectiveStates.Horde:
                    field = HPConst.Map_H[m_TowerType];
                    int horde_towers = ((HellfirePeninsulaPvP)PvP).GetHordeTowersControlled();
                    if (horde_towers != 0)
                        ((HellfirePeninsulaPvP)PvP).SetHordeTowersControlled(--horde_towers);
                    break;
                case ObjectiveStates.NeutralAllianceChallenge:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.NeutralHordeChallenge:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.AllianceHordeChallenge:
                    field = HPConst.Map_A[m_TowerType];
                    break;
                case ObjectiveStates.HordeAllianceChallenge:
                    field = HPConst.Map_H[m_TowerType];
                    break;
            }

            // send world state update
            if (field != 0)
            {
                PvP.SetWorldState(field, 0);
                field = 0;
            }
            int artkit = 21;
            int artkit2 = HPConst.TowerArtKit_N[m_TowerType];
            switch (State)
            {
                case ObjectiveStates.Neutral:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.Alliance:
                    {
                        field = HPConst.Map_A[m_TowerType];
                        artkit = 2;
                        artkit2 = HPConst.TowerArtKit_A[m_TowerType];
                        int alliance_towers = ((HellfirePeninsulaPvP)PvP).GetAllianceTowersControlled();
                        if (alliance_towers < 3)
                            ((HellfirePeninsulaPvP)PvP).SetAllianceTowersControlled(++alliance_towers);
                        PvP.SendDefenseMessage(HPConst.BuffZones[0], HPConst.LangCapture_A[m_TowerType]);
                        break;
                    }
                case ObjectiveStates.Horde:
                    {
                        field = HPConst.Map_H[m_TowerType];
                        artkit = 1;
                        artkit2 = HPConst.TowerArtKit_H[m_TowerType];
                        int horde_towers = ((HellfirePeninsulaPvP)PvP).GetHordeTowersControlled();
                        if (horde_towers < 3)
                            ((HellfirePeninsulaPvP)PvP).SetHordeTowersControlled(++horde_towers);
                        PvP.SendDefenseMessage(HPConst.BuffZones[0], HPConst.LangCapture_H[m_TowerType]);
                        break;
                    }
                case ObjectiveStates.NeutralAllianceChallenge:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.NeutralHordeChallenge:
                    field = HPConst.Map_N[m_TowerType];
                    break;
                case ObjectiveStates.AllianceHordeChallenge:
                    field = HPConst.Map_A[m_TowerType];
                    artkit = 2;
                    artkit2 = HPConst.TowerArtKit_A[m_TowerType];
                    break;
                case ObjectiveStates.HordeAllianceChallenge:
                    field = HPConst.Map_H[m_TowerType];
                    artkit = 1;
                    artkit2 = HPConst.TowerArtKit_H[m_TowerType];
                    break;
            }

            Map map = Global.MapMgr.FindMap(530, 0);
            var bounds = map.GetGameObjectBySpawnIdStore().LookupByKey(m_capturePointSpawnId);
            foreach (var go in bounds)
                go.SetGoArtKit(artkit);

            bounds = map.GetGameObjectBySpawnIdStore().LookupByKey(m_flagSpawnId);
            foreach (var go in bounds)
                go.SetGoArtKit(artkit2);

            // send world state update
            if (field != 0)
                PvP.SetWorldState((int)field, 1);

            // complete quest objective
            if (State == ObjectiveStates.Alliance || State == ObjectiveStates.Horde)
                SendObjectiveComplete(HPConst.CreditMarker[m_TowerType], ObjectGuid.Empty);
        }

        int m_TowerType;
        long m_flagSpawnId;
    }

    [Script]
    class OutdoorPvP_hellfire_peninsula : OutdoorPvPScript
    {
        public OutdoorPvP_hellfire_peninsula() : base("outdoorpvp_hp") { }

        public override OutdoorPvP GetOutdoorPvP(Map map)
        {
            return new HellfirePeninsulaPvP(map);
        }
    }

    struct HPConst
    {
        public static int[] LangCapture_A = { DefenseMessages.BrokenHillTakenAlliance, DefenseMessages.OverlookTakenAlliance, DefenseMessages.StadiumTakenAlliance };

        public static int[] LangCapture_H = { DefenseMessages.BrokenHillTakenHorde, DefenseMessages.OverlookTakenHorde, DefenseMessages.StadiumTakenHorde };

        public static int[] Map_N = { 2485, 2482, 0x9a8 };

        public static int[] Map_A = { 2483, 2480, 2471 };

        public static int[] Map_H = { 2484, 2481, 2470 };

        public static int[] TowerArtKit_A = { 65, 62, 67 };

        public static int[] TowerArtKit_H = { 64, 61, 68 };

        public static int[] TowerArtKit_N = { 66, 63, 69 };

        //  HP, citadel, ramparts, blood furnace, shattered halls, mag's lair
        public static int[] BuffZones = { 3483, 3563, 3562, 3713, 3714, 3836 };

        public static int[] CreditMarker = { 19032, 19028, 19029 };

        public static int[] CapturePointEventEnter = { 11404, 11396, 11388 };

        public static int[] CapturePointEventLeave = { 11403, 11395, 11387 };
    }

    struct DefenseMessages
    {
        public const int OverlookTakenAlliance = 14841; // '|cffffff00The Overlook has been taken by the Alliance!|r'
        public const int OverlookTakenHorde = 14842; // '|cffffff00The Overlook has been taken by the Horde!|r'
        public const int StadiumTakenAlliance = 14843; // '|cffffff00The Stadium has been taken by the Alliance!|r'
        public const int StadiumTakenHorde = 14844; // '|cffffff00The Stadium has been taken by the Horde!|r'
        public const int BrokenHillTakenAlliance = 14845; // '|cffffff00Broken Hill has been taken by the Alliance!|r'
        public const int BrokenHillTakenHorde = 14846; // '|cffffff00Broken Hill has been taken by the Horde!|r'
    }

    struct OutdoorPvPHPSpells
    {
        public const int AlliancePlayerKillReward = 32155;
        public const int HordePlayerKillReward = 32158;
        public const int AllianceBuff = 32071;
        public const int HordeBuff = 32049;
    }

    enum OutdoorPvPHPTowerType
    {
        BrokenHill = 0,
        Overlook = 1,
        Stadium = 2,
        Num = 3
    }

    struct OutdoorPvPHPWorldStates
    {
        public const int Display_A = 0x9ba;
        public const int Display_H = 0x9b9;

        public const int Count_H = 0x9ae;
        public const int Count_A = 0x9ac;
    }
}
