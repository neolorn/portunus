using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Portunus.Janus.Tests;

/// <summary>A logger that keeps each entry's level and message, for a test's check of what was logged.</summary>
internal sealed class CapturedLog : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    /// <summary>The entries logged, in order.</summary>
    internal IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

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
        _entries.Add((logLevel, formatter(state, exception)));
    }
}