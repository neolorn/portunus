using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Extensions.Logging;

using Xunit;

namespace Portunus.Tests;

/// <summary>
/// A logger that keeps every line written to it, so a test reads what the client logged and
/// checks that no line holds a value the test used (<c>05</c> PRT-OPS-005).
/// </summary>
internal sealed class CapturedLog : ILogger
{
    private readonly Lock _lock = new();

    private readonly List<CapturedLine> _lines = [];

    /// <summary>Every line written, in order.</summary>
    internal IReadOnlyList<CapturedLine> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
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
        IReadOnlyList<KeyValuePair<string, object?>> members = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        lock (_lock)
        {
            _lines.Add(new CapturedLine(logLevel, formatter(state, exception), [.. members.Select(member => $"{member.Key}={member.Value}")], exception?.ToString()));
        }
    }

    /// <summary>Fails where a line holds one of <paramref name="values"/>, as its text, its base64 or its base64url.</summary>
    internal void HoldsNone(params byte[][] values)
    {
        foreach (byte[] value in values)
        {
            string[] forms = [Encoding.UTF8.GetString(value), Convert.ToBase64String(value), Base64Url.EncodeToString(value)];
            foreach (CapturedLine line in Lines)
            {
                foreach (string text in line.Texts())
                {
                    Assert.DoesNotContain(forms, form => text.Contains(form, StringComparison.Ordinal));
                }
            }
        }
    }
}

/// <summary>A line of <see cref="CapturedLog"/>: its level, its message, its members and the exception it carried.</summary>
internal sealed record CapturedLine(LogLevel Level, string Message, IReadOnlyList<string> Members, string? Exception)
{
    internal IEnumerable<string> Texts()
    {
        return [Message, .. Members, .. Exception is null ? [] : new[] { Exception }];
    }
}