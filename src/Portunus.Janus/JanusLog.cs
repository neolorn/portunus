using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus.Janus;

/// <summary>The adapter's log entries (<c>05</c> PRT-OPS-005, <c>04</c> PRT-LIB-006): an address and a code, never a value.</summary>
internal static partial class JanusLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "secrets.secret.forminvalid at {Address}")]
    internal static partial void FormInvalid(ILogger logger, SecretAddress address);
}