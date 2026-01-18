#nullable enable
using System;
using Microsoft.Extensions.Logging;

namespace Meadow.Debugging.DAP.Utilities
{
    /// <summary>
    /// A simple ILogger implementation that forwards log messages to a callback.
    /// Used during debug sessions to route Meadow CLI logs to the debug console.
    /// </summary>
    public class DebugSessionLogger : ILogger
    {
        private string _previousFileName = string.Empty;
        private uint _previousPercentage = 0;

        private readonly LogLevel _minLogLevel;

        public DebugSessionLogger(Action<string> callback, LogLevel minLogLevel = LogLevel.Information)
        {
            Callback = callback;
            _minLogLevel = minLogLevel;
        }

        public Action<string> Callback { get; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => default;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                return true;

            return logLevel >= _minLogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            try
            {
                var message = formatter(state, exception);

                Callback?.Invoke(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logging failed: {ex.Message}");
                throw;
            }
        }

        internal void ReportDeviceMessage(string source, string message)
        {
            this.LogInformation($"{source}: {message}");
        }

        internal void ReportFileProgress(string fileName, uint percentage)
        {
            if (percentage > 0
            && percentage > 99)
            {
                if (!_previousFileName.Equals(fileName)
                || !_previousPercentage.Equals(percentage))
                {
                    this.LogInformation($"{percentage}% of '{fileName}' Sent");
                    _previousFileName = fileName;
                    _previousPercentage = percentage;
                }
            }
        }
    }
}
