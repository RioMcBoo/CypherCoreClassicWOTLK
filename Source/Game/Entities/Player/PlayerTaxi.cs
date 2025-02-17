﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Game.DataStorage;
using Game.Networking.Packets;
using System.Collections.Generic;
using System.Text;
using System;

namespace Game.Entities
{
    public class PlayerTaxi
    {
        public byte[] m_taximask;
        List<int> m_TaxiDestinations = new();
        int m_flightMasterFactionId;

        public PlayerTaxi(int taxiMaskSize)
        {
            m_taximask = new byte[taxiMaskSize];
        }

        public void InitTaxiNodesForLevel(Race race, Class chrClass, int level)
        {
            // class specific initial known nodes
            if (chrClass == Class.DeathKnight)
            {
                var factionMask = Player.TeamForRace(race) == Team.Horde ? CliDB.HordeTaxiNodesMask : CliDB.AllianceTaxiNodesMask;
                m_taximask = new byte[factionMask.Length];
                for (int i = 0; i < factionMask.Length; ++i)
                    m_taximask[i] |= (byte)(CliDB.OldContinentsNodesMask[i] & factionMask[i]);
            }

            // race specific initial known nodes: capital and taxi hub masks
            switch (race)
            {
                case Race.Human:
                case Race.Dwarf:
                case Race.NightElf:
                case Race.Gnome:
                case Race.Draenei:
                case Race.Worgen:
                case Race.PandarenAlliance:
                    SetTaximaskNode(2);     // Stormwind, Elwynn
                    SetTaximaskNode(6);     // Ironforge, Dun Morogh
                    SetTaximaskNode(26);    // Lor'danel, Darkshore
                    SetTaximaskNode(27);    // Rut'theran Village, Teldrassil
                    SetTaximaskNode(49);    // Moonglade (Alliance)
                    SetTaximaskNode(94);    // The Exodar
                    break;
                case Race.Orc:
                case Race.Undead:
                case Race.Tauren:
                case Race.Troll:
                case Race.BloodElf:
                case Race.Goblin:
                case Race.PandarenHorde:
                    SetTaximaskNode(11);    // Undercity, Tirisfal
                    SetTaximaskNode(22);    // Thunder Bluff, Mulgore
                    SetTaximaskNode(23);    // Orgrimmar, Durotar
                    SetTaximaskNode(69);    // Moonglade (Horde)
                    SetTaximaskNode(82);    // Silvermoon City
                    break;
            }

            // new continent starting masks (It will be accessible only at new map)
            switch (Player.TeamForRace(race))
            {
                case Team.Alliance:
                    SetTaximaskNode(100);
                    break;
                case Team.Horde:
                    SetTaximaskNode(99);
                    break;
            }
            // level dependent taxi hubs
            if (level >= 68)
                SetTaximaskNode(213);                               //Shattered Sun Staging Area
        }

        public void LoadTaxiMask(string data)
        {
            var split = new StringArray(data, ' ');

            int index = 0;
            for (var i = 0; index < m_taximask.Length && i != split.Length; ++i, ++index)
            {
                // load and set bits only for existing taxi nodes
                if (byte.TryParse(split[i], out byte submask))
                    m_taximask[index] = (byte)(CliDB.TaxiNodesMask[index] & submask);
            }
        }

        public void AppendTaximaskTo(ShowTaxiNodes data, bool all)
        {
            data.CanLandNodes = new byte[CliDB.TaxiNodesMask.Length];
            data.CanUseNodes = new byte[CliDB.TaxiNodesMask.Length];

            if (all)
            {
                Buffer.BlockCopy(CliDB.TaxiNodesMask, 0, data.CanLandNodes, 0, data.CanLandNodes.Length);  // all existed nodes
                Buffer.BlockCopy(CliDB.TaxiNodesMask, 0, data.CanUseNodes, 0, data.CanUseNodes.Length);
            }
            else
            {
                Buffer.BlockCopy(m_taximask, 0, data.CanLandNodes, 0, data.CanLandNodes.Length); // known nodes
                Buffer.BlockCopy(m_taximask, 0, data.CanUseNodes, 0, data.CanUseNodes.Length);
            }
        }

        public bool LoadTaxiDestinationsFromString(string values, Team team)
        {
            ClearTaxiDestinations();

            var stringArray = new StringArray(values, ' ');
            if (stringArray.Length > 0)
                int.TryParse(stringArray[0], out m_flightMasterFactionId);

            for (var i = 1; i < stringArray.Length; ++i)
            {
                if (int.TryParse(stringArray[i], out int node))
                    AddTaxiDestination(node);
            }

            if (m_TaxiDestinations.Empty())
                return true;

            // Check integrity
            if (m_TaxiDestinations.Count < 2)
                return false;

            for (int i = 1; i < m_TaxiDestinations.Count; ++i)
            {
                int path;
                Global.ObjectMgr.GetTaxiPath(m_TaxiDestinations[i - 1], m_TaxiDestinations[i], out path, out _);
                if (path == 0)
                    return false;
            }

            // can't load taxi path without mount set (quest taxi path?)
            if (Global.ObjectMgr.GetTaxiMountDisplayId(GetTaxiSource(), team, true) == 0)
                return false;

            return true;
        }

        public string SaveTaxiDestinationsToString()
        {
            if (m_TaxiDestinations.Empty())
                return "";

            Cypher.Assert(m_TaxiDestinations.Count >= 2);

            StringBuilder ss = new();
            ss.Append($"{m_flightMasterFactionId} ");

            for (int i = 0; i < m_TaxiDestinations.Count; ++i)
                ss.Append($"{m_TaxiDestinations[i]} ");

            return ss.ToString();
        }

        public int GetCurrentTaxiPath()
        {
            if (m_TaxiDestinations.Count < 2)
                return 0;

            int path;

            Global.ObjectMgr.GetTaxiPath(m_TaxiDestinations[0], m_TaxiDestinations[1], out path, out _);

            return path;
        }

        public bool RequestEarlyLanding()
        {
            if (m_TaxiDestinations.Count <= 2)
                return false;

            // start from first destination - m_TaxiDestinations[0] is the current starting node
            for (var i = 1; i < m_TaxiDestinations.Count; ++i)
            {
                if (IsTaximaskNodeKnown(m_TaxiDestinations[i]))
                {
                    if (++i == m_TaxiDestinations.Count - 1)
                        return false;   // if we are left with only 1 known node on the path don't change the spline, its our final destination anyway

                    m_TaxiDestinations.RemoveRange(i, m_TaxiDestinations.Count - i);
                    return true;
                }
            }

            return false;
        }

        public FactionTemplateRecord GetFlightMasterFactionTemplate()
        {
            return CliDB.FactionTemplateStorage.LookupByKey(m_flightMasterFactionId);
        }

        public void SetFlightMasterFactionTemplateId(int factionTemplateId)
        {
            m_flightMasterFactionId = factionTemplateId;
        }

        public bool IsTaximaskNodeKnown(int nodeidx)
        {
            int field = (nodeidx - 1) / 8;
            byte submask = (byte)(1 << ((nodeidx - 1) % 8));
            return (m_taximask[field] & submask) == submask;
        }

        public bool SetTaximaskNode(int nodeidx)
        {
            int field = (nodeidx - 1) / 8;
            byte submask = (byte)(1 << ((nodeidx - 1) % 8));
            if ((m_taximask[field] & submask) != submask)
            {
                m_taximask[field] |= submask;
                return true;
            }
            else
                return false;
        }

        public void ClearTaxiDestinations() { m_TaxiDestinations.Clear(); }
        public void AddTaxiDestination(int dest) { m_TaxiDestinations.Add(dest); }
        void SetTaxiDestination(List<int> nodes)
        {
            m_TaxiDestinations.Clear();
            m_TaxiDestinations.AddRange(nodes);
        }
        public int GetTaxiSource() { return m_TaxiDestinations.Empty() ? 0 : m_TaxiDestinations[0]; }
        public int GetTaxiDestination() { return m_TaxiDestinations.Count < 2 ? 0 : m_TaxiDestinations[1]; }
        public int NextTaxiDestination()
        {
            m_TaxiDestinations.RemoveAt(0);
            return GetTaxiDestination();
        }
        public List<int> GetPath() { return m_TaxiDestinations; }
        public bool Empty() { return m_TaxiDestinations.Empty(); }
    }
}
