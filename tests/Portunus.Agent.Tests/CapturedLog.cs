using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Portunus.Agent.Tests;

/// <summary>A logger provider keeping every line the agent writes, so a test reads what it logged (<c>05</c> PRT-OPS-005).</summary>
internal sealed class CapturedLog : ILoggerProvider, ILogger
{
    private readonly Lock _lock = new();

    private readonly List<string> _lines = [];

    /// <summary>Every line written, its message and its members, in order.</summary>
    internal IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        lock (_lock)
        {
            _lines.Add(formatter(state, exception));
        }
    }

    public void Dispose()
    {
        // The lines outlive the host, so the test reads them after it stops.
    }
}