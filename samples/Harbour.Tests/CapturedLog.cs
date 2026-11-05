using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Harbour.Tests;

/// <summary>A logger provider that keeps every line the harbour writes, so a test reads them and checks that none holds a value it used.</summary>
internal sealed class CapturedLog : ILoggerProvider, ILogger
{
    private readonly Lock _lock = new();

    private readonly List<string> _lines = [];

    /// <summary>Every line written, in order, its exception's text after it where it has one.</summary>
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
            _lines.Add(exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}");
        }
    }

    public void Dispose()
    {
        // The lines outlive the host, so the test reads them after it stops.
    }
}