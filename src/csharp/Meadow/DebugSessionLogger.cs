using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
	public class DebugSessionLogger : ILogger
	{
		public DebugSessionLogger(Action<string> callback)
		{
			Callback = callback;
		}

		public Action<string> Callback { get; }

		public IDisposable BeginScope<TState>(TState state)
			=> default;

		public bool IsEnabled(LogLevel logLevel)
		{
			if (System.Diagnostics.Debugger.IsAttached)
				return true;

			return logLevel >= LogLevel.Information;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (IsEnabled(logLevel))
			{
				Callback?.Invoke(formatter(state, exception));
			}
		}

		internal async Task ReportDeviceMessage(string source, string message)
		{

		}
		internal async Task ReportFileProgress(string fileName, uint percentage)
		{
			if (percentage <= 1)
			{
				this.LogInformation($"Sending {fileName}");
			}
			else if (percentage % 10 == 0)
			{
				this.LogInformation($"{percentage}% of {fileName} Sent");
			}
		}
	}
}