using System;

namespace Meadow.Debugging.Core.Deployment
{
    /// <summary>
    /// Callbacks from deployment process to the debug session.
    /// Replaces direct MonoDebugSession dependency in MeadowDeployer.
    /// </summary>
    public interface IDeploymentCallbacks
    {
        /// <summary>
        /// Report file deployment progress.
        /// </summary>
        /// <param name="fileName">Name of the file being deployed</param>
        /// <param name="completed">Bytes completed</param>
        /// <param name="total">Total bytes</param>
        void OnFileProgress(string fileName, long completed, long total);

        /// <summary>
        /// Report a message received from the device.
        /// </summary>
        /// <param name="source">Message source (e.g., "stdout", "info")</param>
        /// <param name="message">The message content</param>
        void OnDeviceMessage(string source, string message);

        /// <summary>
        /// Report a log message from the deployment process.
        /// </summary>
        /// <param name="message">Log message</param>
        void OnLogMessage(string message);

        /// <summary>
        /// Report an error during deployment.
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="exception">Optional exception</param>
        void OnError(string message, Exception? exception = null);
    }
}
