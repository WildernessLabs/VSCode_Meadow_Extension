#nullable enable
using System;
using Meadow.Debugging.Core.Deployment;
using Meadow.Debugging.Core.Events;

namespace VSCodeDebug
{
    /// <summary>
    /// Adapts IDeploymentCallbacks to IDebugEventEmitter.
    /// Bridges deployment events to debug event emission for VSCode.
    /// </summary>
    internal class DeploymentCallbackAdapter : IDeploymentCallbacks
    {
        private readonly IDebugEventEmitter _eventEmitter;
        private string _previousFileName = string.Empty;
        private uint _previousPercentage = 0;

        public DeploymentCallbackAdapter(IDebugEventEmitter eventEmitter)
        {
            _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
        }

        public void OnFileProgress(string fileName, long completed, long total)
        {
            var percentage = (uint)((completed / (double)total) * 100d);

            // Emit progress bar event
            _eventEmitter.EmitDeploymentProgress(fileName, percentage);

            // Also log when file transfer completes (matches original behavior)
            if (percentage > 99 &&
                (!_previousFileName.Equals(fileName) || !_previousPercentage.Equals(percentage)))
            {
                _eventEmitter.EmitOutput(OutputCategory.Console, $"100% of '{fileName}' Sent\n");
                _previousFileName = fileName;
                _previousPercentage = percentage;
            }
        }

        public void OnDeviceMessage(string source, string message)
        {
            // Route device messages to Meadow output (appears in blue)
            _eventEmitter.EmitDeviceMessage(source, message);
        }

        public void OnLogMessage(string message)
        {
            // Route log messages to console output (appears in yellow)
            _eventEmitter.EmitOutput(OutputCategory.Console, message + Environment.NewLine);
        }

        public void OnError(string message, Exception? exception = null)
        {
            _eventEmitter.EmitOutput(OutputCategory.Console, $"ERROR: {message}\n");
        }
    }
}
