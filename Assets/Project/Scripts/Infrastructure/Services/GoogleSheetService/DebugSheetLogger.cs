using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Services;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

// Only showing errors in debugmenu
public class DebugSheetLogger : ILogger
{
	public static readonly DebugSheetLogger Default = new DebugSheetLogger();

	private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

	private IList<object> scopes = new List<object>();

	public IDisposable BeginScope<TState>(TState state)
	{
		return scopeProvider.Push(state);
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return true;

	}
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
	{
		scopes.Clear();
		scopeProvider.ForEachScope((x, scopes) => scopes.Add(x), scopes);

		var message = formatter(state, exception);
		if (scopes.Count > 0)
			message = $"[{string.Join(">", scopes)}] {message}";

		switch (logLevel)
		{
			case LogLevel.Trace:
			case LogLevel.Debug:
			case LogLevel.Information:
				Debug.Log(message);
				break;
			
			case LogLevel.Warning:
			case LogLevel.Error:
			case LogLevel.Critical:
				// Causes error beacause it's called before initialization of window service
				//AllServices.Container.Single<GameDebugService>().SendDebugLog(message);
				break;
		}
	}
}