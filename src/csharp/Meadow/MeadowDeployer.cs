using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer
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

        Task debugTask = null;

        public async Task Deploy(string folder, int debugPort = -1, Action startedCallback = null)
        {
            if (meadow == null)
            {
                var m = await MeadowDeviceManager.GetMeadowForSerialPort(Serial, logger: Logger)
                    .ConfigureAwait(false);
                if (m == null)
                    throw new InvalidOperationException("Meadow device not found");

                meadow = new MeadowDeviceHelper(m, Logger);
            }
           

            var dllPath = Path.Combine(folder, "App.dll");
            var exePath = Path.Combine(folder, "App.exe");
            if (File.Exists(dllPath))
                File.Copy(dllPath, exePath, true);

            await meadow.MonoDisableAsync(CancelToken).ConfigureAwait(false);
            await meadow.DeployAppAsync(exePath, debugPort > 1000, CancelToken).ConfigureAwait(false);
            await meadow.MonoEnableAsync(CancelToken).ConfigureAwait(false);

            // Debugger only returns when session is done
            debugTask = Task.Run(async () =>
            {
                await meadow.StartDebuggingSessionAsync(debugPort, CancelToken, startedCallback);
            });        
        }
    }
}
