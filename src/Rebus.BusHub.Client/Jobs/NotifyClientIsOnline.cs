﻿using System;
using System.Diagnostics;
using Rebus.BusHub.Messages;
using System.Linq;
using Rebus.BusHub.Messages.Causal;

namespace Rebus.BusHub.Client.Jobs
{
    public class NotifyClientIsOnline : Job
    {
        public override void Initialize(IRebusEvents events, IBusHubClient client)
        {
            events.BusStarted += b =>
                {
                    var currentProcess = Process.GetCurrentProcess();
                    var processStartInfo = currentProcess.StartInfo;
                    var fileName = !string.IsNullOrWhiteSpace(processStartInfo.FileName)
                                       ? processStartInfo.FileName
                                       : currentProcess.ProcessName;

                    var arguments = processStartInfo.Arguments;

                    var entryAssembly = client.GetEntryAssembly();

                    var loadedAssemblies =
                        AppDomain.CurrentDomain
                                 .GetAssemblies()
                                 .Select(a =>
                                     {
                                         var assemblyName = a.GetName();

                                         return
                                             new LoadedAssembly
                                                 {
                                                     Name = assemblyName.Name,
                                                     Location = a.IsDynamic ? "(dynamic)" : a.Location,
                                                     Codebase = a.IsDynamic ? "(dynamic)" : a.CodeBase,
                                                     Version = assemblyName.Version.ToString(),
                                                     IsEntryAssembly = a == entryAssembly
                                                 };
                                     })
                                 .ToArray();

                    SendMessage(new BusHasBeenStarted
                                    {
                                        InputQueueAddress = client.InputQueueAddress,
                                        MachineName = Environment.MachineName,
                                        Os = Environment.OSVersion.ToString(),
                                        FileName = fileName,
                                        Arguments = arguments,
                                        LoadedAssemblies = loadedAssemblies
                                    });
                };
        }
    }
}