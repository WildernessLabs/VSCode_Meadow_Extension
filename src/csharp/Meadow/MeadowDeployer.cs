﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using VSCodeDebug;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer : IDisposable
    {
        public MeadowDeployer(MonoDebugSession monoDebugSession, ILogger logger, string serial, CancellationToken cancellationToken)
        {
            Logger = logger;
            Serial = serial;
            CancelToken = cancellationToken;
            DebugSession = monoDebugSession;
        }

        public ILogger Logger { get; private set; }
        public string Serial { get; private set; }
        public CancellationToken CancelToken { get; private set; }

        public MonoDebugSession DebugSession { get; private set; }

        IMeadowConnection meadowConnection = null;

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

        public async Task<DebuggingServer> Deploy(string folder, int debugPort = -1)
        {
            if (meadowConnection == null)
            {
                var retryCount = 0;

            get_serial_connection:
                try
                {
                    meadowConnection = new SerialConnection(Serial, Logger);
                }
                catch
                {
                    retryCount++;
                    if (retryCount > 10)
                    {
                        throw new Exception($"Cannot find port {Serial}");
                    }
                    System.Threading.Thread.Sleep(500);
                    goto get_serial_connection;
                }

                string path = folder ?? Environment.CurrentDirectory;

                await meadowConnection?.WaitForMeadowAttach();
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

            var isDebugging = debugPort > 1000;
            meadowConnection.FileWriteProgress += MeadowConnection_DeploymentProgress;

            try
            {
                await AppManager.DeployApplication(null, meadowConnection, folder, isDebugging, false, Logger, CancelToken);
            }
            finally
            {
                meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
            }

            // Debugger only returns when session is done
            if (isDebugging)
                return await meadowConnection?.StartDebuggingSession(debugPort, Logger, CancelToken);

            return null;
        }

        private void MeadowConnection_DeploymentProgress(object sender, (string fileName, long completed, long total) e)
        {
            var p = (uint)((e.completed / (double)e.total) * 100d);
            Logger?.LogInformation($"Transferring {e.fileName}");
            // TODO Uncomment once Server to Client messaging is working - DebugSession.SendEvent(new UpdateProgressBarEvent(e.fileName, p));
        }
    }
}