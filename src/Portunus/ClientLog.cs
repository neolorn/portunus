using System;

using Microsoft.Extensions.Logging;

namespace Portunus;

/// <summary>
/// What the client logs (<c>05</c> PRT-OPS-005): its waits and each failure it answers, by an
/// address, a reason and a code, never a value.
/// </summary>
internal static partial class ClientLog
{
    /// <summary>A read waits for the agent (<c>04</c> PRT-LIB-004), at the first wait and then at most every 30 seconds.</summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "waiting for secrets: reason {Reason}, waited {Waited}")]
    internal static partial void WaitingForSecrets(ILogger logger, string reason, TimeSpan waited);

    /// <summary>A read answered a failure.</summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "read of {Address} failed: {Code}")]
    internal static partial void ReadFailed(ILogger logger, string address, string code);

    /// <summary>A status answered a failure.</summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "status failed: {Code}")]
    internal static partial void StatusFailed(ILogger logger, string code);
}