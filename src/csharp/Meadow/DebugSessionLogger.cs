using System;
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
			=> logLevel != LogLevel.Trace && logLevel != LogLevel.Debug;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			=> Callback?.Invoke(formatter(state, exception));
	}
}
