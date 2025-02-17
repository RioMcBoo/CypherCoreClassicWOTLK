﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Game.Networking
{
    public static class PacketManager
    {
        public static void Initialize()
        {
            Assembly currentAsm = Assembly.GetExecutingAssembly();
            foreach (var type in currentAsm.GetTypes())
            {
                foreach (var methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    foreach (var msgAttr in methodInfo.GetCustomAttributes<WorldPacketHandlerAttribute>())
                    {
                        if (msgAttr == null)
                            continue;

                        if (msgAttr.Opcode == ClientOpcodes.Unknown)
                        {
                            Log.outError(LogFilter.Network,
                                $"{methodInfo.Name} has Attribute with unused opcode.");
                            continue;
                        }

                        if (_clientPacketTable.ContainsKey(msgAttr.Opcode))
                        {
                            Log.outError(LogFilter.Network, 
                                $"Tried to override OpcodeHandler of {_clientPacketTable[msgAttr.Opcode]} " +
                                $"with {methodInfo.Name} (Opcode {msgAttr.Opcode})");
                            continue;
                        }

                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length == 0)
                        {
                            Log.outError(LogFilter.Network,
                                $"Method: {methodInfo.Name} Has no paramters");
                            continue;
                        }

                        if (parameters[0].ParameterType.BaseType != typeof(ClientPacket))
                        {
                            Log.outError(LogFilter.Network, 
                                $"Method: {methodInfo.Name} has wrong BaseType");
                            continue;
                        }

                        _clientPacketTable[msgAttr.Opcode] = 
                            new PacketHandler(methodInfo, msgAttr.Status, msgAttr.Processing, parameters[0].ParameterType);
                    }
                }
            }
        }

        public static bool TryPeek(this ConcurrentQueue<WorldPacket> queue, out WorldPacket result, PacketFilter filter)
        {
            result = null;

            if (queue.IsEmpty)
                return false;

            if (!queue.TryPeek(out result))
                return false;

            if (!filter.Process(result))
                return false;

            return true;
        }

        public static PacketHandler GetHandler(ClientOpcodes opcode)
        {
            return _clientPacketTable.LookupByKey(opcode);
        }

        public static bool ContainsHandler(ClientOpcodes opcode)
        {
            return _clientPacketTable.ContainsKey(opcode);
        }

        static ConcurrentDictionary<ClientOpcodes, PacketHandler> _clientPacketTable = new();

        public static bool IsInstanceOnlyOpcode(ServerOpcodes opcode)
        {
            switch (opcode)
            {
                case ServerOpcodes.QuestGiverStatus: // ClientQuest
                case ServerOpcodes.DuelRequested: // Client
                case ServerOpcodes.DuelInBounds: // Client
                case ServerOpcodes.QueryTimeResponse: // Client
                case ServerOpcodes.DuelWinner: // Client
                case ServerOpcodes.DuelComplete: // Client
                case ServerOpcodes.DuelOutOfBounds: // Client
                case ServerOpcodes.AttackStop: // Client
                case ServerOpcodes.AttackStart: // Client
                case ServerOpcodes.MountResult: // Client
                    return true;
                default:
                    return false;
            }
        }
    }

    public class PacketHandler
    {
        public PacketHandler(MethodInfo info, SessionStatus status, PacketProcessing processingplace, Type type)
        {
            methodCaller = (Action<WorldSession, ClientPacket>)GetType().GetMethod(
                "CreateDelegate", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(type).Invoke(null, [info]);

            sessionStatus = status;
            ProcessingPlace = processingplace;
            packetType = type;
        }

        public void Invoke(WorldSession session, WorldPacket packet)
        {
            if (packetType == null)
                return;

            using var clientPacket = (ClientPacket)Activator.CreateInstance(packetType, packet);
            clientPacket.Read();
            clientPacket.LogPacket(session);
            methodCaller(session, clientPacket);
        }

        static Action<WorldSession, ClientPacket> CreateDelegate<P1>(MethodInfo method) where P1 : ClientPacket
        {
            // create first delegate. It is not fine because its 
            // signature contains unknown types T and P1
            Action<WorldSession, P1> d = (Action<WorldSession, P1>)method.CreateDelegate(typeof(Action<WorldSession, P1>));
            // create another delegate having necessary signature. 
            // It encapsulates first delegate with a closure
            return delegate (WorldSession target, ClientPacket p) { d(target, (P1)p); };
        }

        Action<WorldSession, ClientPacket> methodCaller;
        Type packetType;
        public PacketProcessing ProcessingPlace { get; private set; }
        public SessionStatus sessionStatus { get; private set; }
    }

    public abstract class PacketFilter
    {
        protected PacketFilter(WorldSession pSession)
        {
            m_pSession = pSession;
        }

        public abstract bool Process(WorldPacket packet);

        public virtual bool ProcessUnsafe() { return false; }

        protected WorldSession m_pSession;
    }

    public class MapSessionFilter : PacketFilter
    {
        public MapSessionFilter(WorldSession pSession) : base(pSession) { }

        public override bool Process(WorldPacket packet)
        {
            PacketHandler opHandle = PacketManager.GetHandler((ClientOpcodes)packet.GetOpcode());
            //check if packet handler is supposed to be safe
            if (opHandle.ProcessingPlace == PacketProcessing.Inplace)
                return true;

            //we do not process thread-unsafe packets
            if (opHandle.ProcessingPlace == PacketProcessing.ThreadUnsafe)
                return false;

            Player player = m_pSession.GetPlayer();
            if (player == null)
                return false;

            //in Map.Update() we do not process packets where player is not in world!
            return player.IsInWorld;
        }
    }

    public class WorldSessionFilter : PacketFilter
    {
        public WorldSessionFilter(WorldSession pSession) : base(pSession) { }

        public override bool Process(WorldPacket packet)
        {
            PacketHandler opHandle = PacketManager.GetHandler((ClientOpcodes)packet.GetOpcode());
            //check if packet handler is supposed to be safe
            if (opHandle.ProcessingPlace == PacketProcessing.Inplace)
                return true;

            //thread-unsafe packets should be processed in World.UpdateSessions()
            if (opHandle.ProcessingPlace == PacketProcessing.ThreadUnsafe)
                return true;

            //no player attached? . our client! ^^
            Player player = m_pSession.GetPlayer();
            if (player == null)
                return true;

            //lets process all packets for non-in-the-world player
            return !player.IsInWorld;
        }

        public override bool ProcessUnsafe() { return true; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class WorldPacketHandlerAttribute : Attribute
    {
        public WorldPacketHandlerAttribute(ClientOpcodes opcode)
        {
            Opcode = opcode;
            Status = SessionStatus.Loggedin;
            Processing = PacketProcessing.ThreadUnsafe;
        }

        public ClientOpcodes Opcode { get; private set; }
        public SessionStatus Status { get; set; }
        public PacketProcessing Processing { get; set; }
    }
}
