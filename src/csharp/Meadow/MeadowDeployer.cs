#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Debugging.Core.Deployment;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
    /// <summary>
    /// Deploys applications to Meadow devices.
    /// Implements IDeploymentOrchestrator for use by any IDE (VSCode, Visual Studio, Rider).
    /// </summary>
    public class MeadowDeployer : IDeploymentOrchestrator
    {
        private readonly IDeploymentCallbacks _callbacks;
        private readonly ILogger _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly SettingsManager _settingsManager;
        private readonly MeadowConnectionManager _connectionManager;

        private IMeadowConnection? _meadowConnection;
        private bool _disposed;

        public string PortName { get; }

        /// <summary>
        /// Create a new MeadowDeployer.
        /// </summary>
        /// <param name="callbacks">Callbacks for deployment events</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="portName">Serial port name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public MeadowDeployer(
            IDeploymentCallbacks callbacks,
            ILogger logger,
            string portName,
            CancellationToken cancellationToken)
        {
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PortName = portName ?? throw new ArgumentNullException(nameof(portName));
            _cancellationToken = cancellationToken;

            _settingsManager = new SettingsManager();
            _connectionManager = new MeadowConnectionManager(_settingsManager);
        }

        public async Task<IMeadowConnection?> DeployAsync(string folder, bool isDebugging)
        {
            // Clean up previous connection
            if (_meadowConnection != null)
            {
                _meadowConnection.FileWriteProgress -= OnFileWriteProgress;
                _meadowConnection.DeviceMessageReceived -= OnDeviceMessageReceived;
                _meadowConnection = null;
            }

            _logger.LogInformation("Connecting to Meadow...");

            _meadowConnection = _connectionManager.GetConnection(PortName);

            if (_meadowConnection == null)
            {
                _callbacks.OnError("No Meadow Connection available.");
                _logger.LogError("No Meadow Connection available.");
                return null;
            }

            _meadowConnection.FileWriteProgress += OnFileWriteProgress;
            _meadowConnection.DeviceMessageReceived += OnDeviceMessageReceived;

            try
            {
                _logger.LogInformation("Checking runtime state...");
                await _meadowConnection.WaitForMeadowAttach(_cancellationToken);

                if (await _meadowConnection.IsRuntimeEnabled(_cancellationToken))
                {
                    _logger.LogInformation("Disabling runtime...");
                    await _meadowConnection.RuntimeDisable(_cancellationToken);
                }

                var deviceInfo = await _meadowConnection.GetDeviceInfo(_cancellationToken);
                string osVersion = deviceInfo?.OsVersion ?? string.Empty;

                _logger.LogInformation($"Found Meadow with OS v{osVersion}");

                var fileManager = new FileManager(null!);
                await fileManager.Refresh();

                var packageManager = new PackageManager(fileManager);

                await packageManager.TrimApplication(
                    new FileInfo(Path.Combine(folder, "App.dll")),
                    osVersion,
                    isDebugging,
                    cancellationToken: _cancellationToken);

                await AppManager.DeployApplication(
                    packageManager,
                    _meadowConnection,
                    osVersion,
                    folder,
                    isDebugging,
                    false,
                    _logger,
                    _cancellationToken);

                // Required delay before runtime enable
                await Task.Delay(1500, _cancellationToken);

                await _meadowConnection.RuntimeEnable(_cancellationToken);

                return _meadowConnection;
            }
            catch (Exception ex)
            {
                _callbacks.OnError($"Deployment failed: {ex.Message}", ex);
                throw;
            }
            finally
            {
                if (_meadowConnection != null)
                {
                    _meadowConnection.FileWriteProgress -= OnFileWriteProgress;
                }
            }
        }

        private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
        {
            _callbacks.OnFileProgress(e.fileName, e.completed, e.total);
        }

        private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
        {
            _callbacks.OnDeviceMessage(e.source ?? "unknown", e.message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_meadowConnection != null)
                    {
                        _meadowConnection.FileWriteProgress -= OnFileWriteProgress;
                        _meadowConnection.DeviceMessageReceived -= OnDeviceMessageReceived;
                        try
                        {
                            _meadowConnection.RuntimeDisable(_cancellationToken).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // Ignore errors during cleanup
                        }
                        _meadowConnection = null;
                    }
                }
                _disposed = true;
            }
        }
    }
}
