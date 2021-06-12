using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
    public class MeadowDeployer
    {
        public MeadowSerialDevice MeadowDevice { get; private set; }

        public MeadowDeployer(MeadowSerialDevice meadowSerialDevice)
        {
            MeadowDevice = meadowSerialDevice;
        }

        public async Task Deploy(MeadowSerialDevice meadow, CancellationTokenSource cts, string folder, bool debug = false)
        {
            var dllPath = Path.Combine(folder, "App.dll");
            if (File.Exists(dllPath))
                File.Move(dllPath, Path.Combine(folder, "App.exe"));

            await meadow.MonoDisableAsync(cts.Token).ConfigureAwait(false);
            await meadow.DeployAppAsync(Path.Combine(folder, "App.exe"), debug, cts.Token);
            await meadow.MonoEnableAsync(cts.Token).ConfigureAwait(false);
        }
    }
}
