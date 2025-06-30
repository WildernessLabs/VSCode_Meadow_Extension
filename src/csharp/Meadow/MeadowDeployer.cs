using System;
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

        private bool disposed = false;

        public MeadowDeployer(MonoDebugSession monoDebugSession, ILogger logger, string portName, CancellationToken cancellationToken)
        {
            Logger = logger;
            PortName = portName;
            CancelToken = cancellationToken;
            DebugSession = monoDebugSession;
            this.connectionManager = new MeadowConnectionManager(settingsManager);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    meadowConnection?.RuntimeDisable(CancelToken).GetAwaiter().GetResult();
                    meadowConnection = null;
                }
                // Dispose unmanaged resources
                disposed = true;
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
            
            Logger?.LogInformation("Connecting to Meadow...");
            meadowConnection = connectionManager.GetConnection(PortName);

            if (await meadowConnection.IsRuntimeEnabled(CancelToken))
            {
                Logger?.LogInformation("Disabling runtime...");
                await meadowConnection.RuntimeDisable(CancelToken);
            }

            var deviceInfo = await meadowConnection?.GetDeviceInfo(CancelToken);
            string osVersion = deviceInfo?.OsVersion;
            Logger?.LogInformation($"Found Meadow with OS v{osVersion}");

            var fileManager = new FileManager(null);
            await fileManager.Refresh();

            try
            {
                var packageManager = new PackageManager(fileManager);

                Logger.LogInformation("Trimming application binaries...");
                await packageManager.TrimApplication(new FileInfo(Path.Combine(folder, "App.dll")), osVersion, isDebugging, cancellationToken: CancelToken);

                Logger.LogInformation("Deploying application...");
                await AppManager.DeployApplication(packageManager, meadowConnection, osVersion, folder, isDebugging, false, Logger, CancelToken);

                await packageManager.TrimApplication(new FileInfo(Path.Combine(folder, "App.dll")), osVersion, isDebugging, cancellationToken: CancelToken);

                await AppManager.DeployApplication(packageManager, meadowConnection, osVersion, folder, isDebugging, false, Logger, CancelToken);

                //FIXME: without this delay, the debugger will fail to connect
                await Task.Delay(1500, CancelToken);

                await meadowConnection.RuntimeEnable(CancelToken);
            }
            finally
            {
                meadowConnection.FileWriteProgress -= MeadowConnection_DeploymentProgress;
            }
            return meadowConnection;
        }

        private void MeadowConnection_DeviceMessageReceived(object sender, (string message, string source) e)
        {
            if (Logger is DebugSessionLogger logger)
            {
                logger.ReportDeviceMessage(e.source, e.message);
            }
        }

        private void MeadowConnection_DeploymentProgress(object sender, (string fileName, long completed, long total) e)
        {
            var p = (uint)((e.completed / (double)e.total) * 100d);

            if (Logger is DebugSessionLogger logger)
            {
                logger.ReportFileProgress(e.fileName, p);
            }

            // Send progress update to VSCode
            DebugSession.SendEvent(new UpdateProgressBarEvent(e.fileName, p));
        }
    }
}