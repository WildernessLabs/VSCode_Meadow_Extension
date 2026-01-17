using System;
using System.Threading;
using System.Threading.Tasks;
using Meadow.Hcom;

namespace Meadow.Debugging.Core.Deployment
{
    /// <summary>
    /// Orchestrates deployment of applications to Meadow devices.
    /// </summary>
    public interface IDeploymentOrchestrator : IDisposable
    {
        /// <summary>
        /// Deploy an application to the connected Meadow device.
        /// </summary>
        /// <param name="outputFolder">Path to the build output folder</param>
        /// <param name="isDebugging">Whether debugging is enabled</param>
        /// <returns>Connection that can be used for debugging, or null if deployment failed</returns>
        Task<IMeadowConnection?> DeployAsync(string outputFolder, bool isDebugging);

        /// <summary>
        /// The serial port/device identifier.
        /// </summary>
        string PortName { get; }
    }
}
