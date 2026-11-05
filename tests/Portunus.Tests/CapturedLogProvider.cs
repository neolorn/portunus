using Microsoft.Extensions.Logging;

namespace Portunus.Tests;

/// <summary>A logger provider whose every logger writes to one <see cref="CapturedLog"/>, for a host a test runs.</summary>
internal sealed class CapturedLogProvider(CapturedLog log) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return log;
    }

    public void Dispose()
    {
        // The captured log outlives the host, so the test reads it after the host stops.
    }
}