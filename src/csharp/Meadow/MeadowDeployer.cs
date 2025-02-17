﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow;
using Meadow.CLI;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Cloud.Client;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using VSCodeDebug;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer : IDisposable
    {
        public ILogger Logger { get; private set; }
        public string PortName { get; private set; }
        public CancellationToken CancelToken { get; private set; }

        public MonoDebugSession DebugSession { get; private set; }

        private readonly SettingsManager settingsManager = new SettingsManager();
        private readonly MeadowConnectionManager connectionManager = null;

        IMeadowConnection meadowConnection = null;

        public MeadowDeployer(MonoDebugSession monoDebugSession, ILogger logger, string portName, CancellationToken cancellationToken)
        {
            Logger = logger;
            PortName = portName;
            CancelToken = cancellationToken;
            DebugSession = monoDebugSession;
            this.connectionManager = new MeadowConnectionManager(settingsManager);
        }

        public async void Dispose()
        {
            try
            {
                await meadowConnection?.RuntimeDisable(CancelToken);
            }
            catch
            {
            }
            finally
            {
                meadowConnection = null;
            }
        }

        public async Task<IMeadowConnection> Deploy(string folder, bool isDebugging)
        {
                if (meadowConnection != null)
                {
                    meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
                    meadowConnection.DeviceMessageReceived -= MeadowConnection_DeviceMessageReceived;
                    meadowConnection = null;
                }

                meadowConnection = connectionManager.GetConnectionForRoute(PortName);

                meadowConnection.FileWriteProgress += MeadowConnection_DeploymentProgress;
                meadowConnection.DeviceMessageReceived += MeadowConnection_DeviceMessageReceived;

                await meadowConnection.WaitForMeadowAttach();

                if (await meadowConnection.IsRuntimeEnabled() == true)
                {
                    await meadowConnection.RuntimeDisable();
                }

                var deviceInfo = await meadowConnection?.GetDeviceInfo(CancelToken);
                string osVersion = deviceInfo?.OsVersion;

                var fileManager = new FileManager(null);
                await fileManager.Refresh();

                var collection = fileManager.Firmware["Meadow F7"];

                //wrap this is a try/catch so it doesn't crash if the developer is offline
                try
                {
                    // TODO Download OS once we have a valie MeadowCloudClient
                }
                catch (Exception e)
                {
                    Logger?.LogInformation($"OS download failed, make sure you have an active internet connection.{Environment.NewLine}{e.Message}");
                }

                try
                {
                    var packageManager = new PackageManager(fileManager);

                    Logger.LogInformation("Trimming application binaries...");
                    await packageManager.TrimApplication(new FileInfo(Path.Combine(folder, "App.dll")), osVersion, isDebugging, cancellationToken: CancelToken);

                    Logger.LogInformation("Deploying application...");
                    await AppManager.DeployApplication(packageManager, meadowConnection, osVersion, folder, isDebugging, false, Logger, CancelToken);

                    //FIXME: without this delay, the debugger will fail to connect
                    await Task.Delay(1500);

                    await meadowConnection.RuntimeEnable();
                }
                finally
                {
                    meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
                }
                return meadowConnection;
        }

        private async void MeadowConnection_DeviceMessageReceived(object sender, (string message, string source) e)
        {
            if (Logger is DebugSessionLogger logger)
            {
                await logger.ReportDeviceMessage(e.source, e.message);
            }
        }

        private async void MeadowConnection_DeploymentProgress(object sender, (string fileName, long completed, long total) e)
        {
            var p = (uint)((e.completed / (double)e.total) * 100d);

            if (Logger is DebugSessionLogger logger)
            {
                await logger.ReportFileProgress(e.fileName, p);
            }
 
            // TODO DebugSession.SendEvent(new UpdateProgressBarEvent(e.fileName, p));
        }
    }
}