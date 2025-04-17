// -----------------------------------------------------------------------
// <copyright file="WrappedLogger.cs" company="Petabridge, LLC">
//      Copyright (C) 2025 - 2025 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

namespace Incrementalist;

/// <summary>
/// Used to help pass in some additional logging context
/// </summary>
internal sealed class WrappedLogger : ILogger
{
    private readonly ILogger _logger;
    private readonly string _name;

    public WrappedLogger(ILogger logger, string name)
    {
        _logger = logger;
        _name = name;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, WrappedFormatter);
        return;

        string WrappedFormatter(TState s, Exception? e) => $"[{_name}]{formatter(s, e)}";
    }
}