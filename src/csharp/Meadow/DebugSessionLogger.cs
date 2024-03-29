﻿using System;
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

			return logLevel == LogLevel.Information;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (logLevel == LogLevel.Information || logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
				Callback?.Invoke(formatter(state, exception)?.Trim());
		}
	}
}
