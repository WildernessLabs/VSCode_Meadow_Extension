using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VsCodeMeadowUtil
{
	public class DebugSessionLogger : ILogger
	{
		string previousFileName = string.Empty;
		uint previousPercentage = 0;

		private readonly LogLevel _minLogLevel;

		public DebugSessionLogger(Action<string> callback, LogLevel minLogLevel = LogLevel.Information)
		{
			Callback = callback;
			_minLogLevel = minLogLevel;
		}

		public Action<string> Callback { get; }

		public IDisposable BeginScope<TState>(TState state)
			=> default;

		public bool IsEnabled(LogLevel logLevel)
		{
			if (System.Diagnostics.Debugger.IsAttached)
				return true;

			return logLevel >= _minLogLevel;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!IsEnabled(logLevel)) {
				return;
			}

			if(formatter == null) {
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

		internal async Task ReportDeviceMessage(string source, string message)
		{
			await Task.Run(() => this.LogInformation($"{source}: {message}"));
		}

		internal async Task ReportFileProgress(string fileName, uint percentage)
		{
			if (percentage > 0 
			&& percentage > 99)
			{
				if (!previousFileName.Equals(fileName)
				|| !previousPercentage.Equals(percentage))
				{
					await Task.Run(() => this.LogInformation($"{percentage}% of '{fileName}' Sent"));
					previousFileName = fileName;
					previousPercentage = percentage;
				}
			}
		}
	}
}