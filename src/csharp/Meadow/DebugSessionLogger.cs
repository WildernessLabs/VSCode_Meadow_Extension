using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
	public class DebugSessionLogger : ILogger
	{
		string previousFileName = string.Empty;
		uint previousPercentage = 0;

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
			this.LogInformation($"{source}: {message}");
		}

		internal async Task ReportFileProgress(string fileName, uint percentage)
		{
			if (percentage > 0 
			&& percentage % 10 == 0)
			{
				if (!previousFileName.Equals(fileName)
				|| !previousPercentage.Equals(percentage))
				{
					this.LogInformation($"{percentage}% of {fileName} Sent");
					previousFileName = fileName;
					previousPercentage = percentage;
				}
			}
		}
	}
}