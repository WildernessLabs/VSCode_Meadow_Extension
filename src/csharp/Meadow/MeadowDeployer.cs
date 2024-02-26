using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.Deployment;
using Meadow.Hcom;
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
            Console.WriteLine("In Deploy");
            if (meadowConnection == null)
            {
                Console.WriteLine("Creating SettingsManager");
                var sm = new SettingsManager();

                Console.WriteLine("Gettting Route");
                var route = sm.GetSetting(SettingsManager.PublicSettings.Route);

                Console.WriteLine($"Current Route:{route}");
                if (route == null)
                {
                    throw new Exception($"No 'route' configuration set.{Environment.NewLine}Use the `meadow config route` command. For example:{Environment.NewLine}  > meadow config route COM5");
                }

                var retryCount = 0;

                Console.WriteLine($"get_serial_connection");
            get_serial_connection:
                try
                {
                    meadowConnection = new SerialConnection(route);
                }
                catch
                {
                    retryCount++;
                    if (retryCount > 10)
                    {
                        throw new Exception($"Cannot find port {route}");
                    }
                    Thread.Sleep(500);
                    goto get_serial_connection;
                }

                string path = folder ?? Environment.CurrentDirectory;

                // is the path a file?
                //FileInfo file;

                var lastFile = string.Empty;
            }

            // TODO not working reliably enough for RC1, will investigate further // if (meadow.DeviceAndAppVersionsMatch(appPathDll))
            {
                //wrap this is a try/catch so it doesn't crash if the developer is offline
                try
                {
                    string osVersion = (await meadowConnection.GetDeviceInfo(CancelToken)).OsVersion;

                    // TODO await new DownloadManager(Logger).DownloadOsBinaries(osVersion);
                }
                catch (Exception e)
                {
                    Logger.LogInformation($"OS download failed, make sure you have an active internet connection.{Environment.NewLine}{e.Message}");
                }

                var isDebugging = debugPort > 1000;
                meadowConnection.FileWriteProgress += DeployFileProgress;

                try
                {
                    await AppManager.DeployApplication(null, meadowConnection, folder, isDebugging, false, Logger, CancelToken);
                }
                finally
                {
                    meadowConnection.FileWriteProgress -= DeployFileProgress;
                }

                // Debugger only returns when session is done
                if (isDebugging)
                    return await meadowConnection?.StartDebuggingSession(debugPort, Logger, CancelToken);
            }

            return null;
        }

        private void DeployFileProgress(object sender, (string fileName, long completed, long total) e)
        {
            Console.WriteLine($"Transferrring: {e.fileName}");
        }
    }
}