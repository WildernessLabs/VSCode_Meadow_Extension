﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer : IDisposable
    {
        public MeadowDeployer(ILogger logger, string serial, CancellationToken cancellationToken)
        {
            Logger = logger;
            Serial = serial;
            CancelToken = cancellationToken;
        }

        public ILogger Logger { get; private set; }
        public string Serial { get; private set; }
        public CancellationToken CancelToken { get; private set; }

        MeadowDeviceHelper meadow = null;

        public async void Dispose()
        {
            try
            {
                if (meadow != null)
                    await meadow.MonoDisableAsync(true, CancelToken);
            } catch { }

            try { meadow?.Dispose(); }
            finally { meadow = null; }
        }
        public async Task<DebuggingServer> Deploy(string folder, int debugPort = -1)
        {
            if (meadow == null)
            {
                var m = await MeadowDeviceManager.GetMeadowForSerialPort(Serial, logger: Logger).ConfigureAwait(false);
                if (m == null)
                    throw new InvalidOperationException("Meadow device not found");

                meadow = new MeadowDeviceHelper(m, Logger);
            }

            var appPathDll = Path.Combine(folder, "App.dll");

            if (meadow.DeviceAndAppVersionsMatch(appPathDll))
            {
                //wrap this is a try/catch so it doesn't crash if the developer is offline
                try
                {
                    string osVersion = await meadow.GetOSVersion(TimeSpan.FromSeconds(30), CancelToken)
                        .ConfigureAwait(false);

                    await new DownloadManager(Logger).DownloadLatestAsync(osVersion)
                        .ConfigureAwait(false);
                }
                catch
                {
                    Logger.LogInformation("OS download failed, make sure you have an active internet connection");
                }

                var isDebugging = debugPort > 1000;
                await meadow.DeployAppAsync(appPathDll, isDebugging, CancelToken).ConfigureAwait(false);

                // Debugger only returns when session is done
                if (isDebugging)
                    return await meadow.StartDebuggingSessionAsync(debugPort, CancelToken);
            }

            return null;
        }
    }
}
