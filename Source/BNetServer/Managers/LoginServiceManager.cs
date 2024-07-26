﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using BNetServer.Networking;
using Framework.Configuration;
using Framework.Constants;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace BNetServer
{
    public class LoginServiceManager : Singleton<LoginServiceManager>
    {
        ConcurrentDictionary<(uint ServiceHash, uint MethodId), BnetServiceHandler> serviceHandlers = new();
        X509Certificate2 certificate;

        LoginServiceManager() { }

        public void Initialize()
        {
            certificate = new X509Certificate2(ConfigMgr.GetDefaultValue("CertificatesFile", "./BNetServer.pfx"));

            Assembly currentAsm = Assembly.GetExecutingAssembly();
            foreach (var type in currentAsm.GetTypes())
            {
                foreach (var methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    foreach (var serviceAttr in methodInfo.GetCustomAttributes<ServiceAttribute>())
                    {
                        if (serviceAttr == null)
                            continue;

                        var key = (serviceAttr.ServiceHash, serviceAttr.MethodId);
                        if (serviceHandlers.ContainsKey(key))
                        {
                            Log.outError(LogFilter.Network, 
                                $"Tried to override ServiceHandler: {serviceHandlers[key]} " +
                                $"with {methodInfo.Name} (ServiceHash: {serviceAttr.ServiceHash} MethodId: {serviceAttr.MethodId})");
                            continue;
                        }

                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length == 0)
                        {
                            Log.outError(LogFilter.Network, $"Method: {methodInfo.Name} needs at least one parameter");
                            continue;
                        }

                        serviceHandlers[key] = new BnetServiceHandler(methodInfo, parameters);
                    }
                }
            }
        }

        public BnetServiceHandler GetHandler(uint serviceHash, uint methodId)
        {
            return serviceHandlers.LookupByKey((serviceHash, methodId));
        }

        public X509Certificate2 GetCertificate()
        {
            return certificate;
        }
    }

    public class BnetServiceHandler
    {
        Delegate methodCaller;
        Type requestType;
        Type responseType;

        public BnetServiceHandler(MethodInfo info, ParameterInfo[] parameters)
        {
            requestType = parameters[0].ParameterType;
            if (parameters.Length > 1)
                responseType = parameters[1].ParameterType;

            if (responseType != null)
                methodCaller = info.CreateDelegate(Expression.GetDelegateType([typeof(Session), requestType, responseType, info.ReturnType]));
            else
                methodCaller = info.CreateDelegate(Expression.GetDelegateType([typeof(Session), requestType, info.ReturnType]));
        }

        public void Invoke(Session session, uint token, CodedInputStream stream)
        {
            var request = (IMessage)Activator.CreateInstance(requestType);
            request.MergeFrom(stream);

            BattlenetRpcErrorCode status;
            if (responseType != null)
            {
                var response = (IMessage)Activator.CreateInstance(responseType);
                status = (BattlenetRpcErrorCode)methodCaller.DynamicInvoke(session, request, response);

                Log.outDebug(LogFilter.ServiceProtobuf, 
                    $"{session.GetClientInfo()} Client called server Method: {request}) Returned: {response} Status: {status}.");

                if (status == 0)
                    session.SendResponse(token, response);
                else
                    session.SendResponse(token, status);
            }
            else
            {
                status = (BattlenetRpcErrorCode)methodCaller.DynamicInvoke(session, request);
                
                Log.outDebug(LogFilter.ServiceProtobuf, 
                    $"{session.GetClientInfo()} Client called server Method: {request}) Status: {status}.");

                if (status != 0)
                    session.SendResponse(token, status);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ServiceAttribute : Attribute
    {
        public uint ServiceHash { get; set; }
        public uint MethodId { get; set; }

        public ServiceAttribute(OriginalHash serviceHash, uint methodId)
        {
            ServiceHash = (uint)serviceHash;
            MethodId = methodId;
        }
    }
}