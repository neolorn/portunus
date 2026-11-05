using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus.Themis;

/// <summary>The adapter's log entries (<c>05</c> PRT-OPS-005, <c>04</c> PRT-LIB-006): a secret's name and address, never a value.</summary>
internal static partial class ThemisLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Themis's secret {Name} at {Address}: secrets.secret.denied")]
    internal static partial void Denied(ILogger logger, string name, SecretAddress address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Themis's secret {Name} at {Address}, which the table does not list, is read as UTF-8 text")]
    internal static partial void Unlisted(ILogger logger, string name, SecretAddress address);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Themis's secret {Name} is outside the secret name's rule, and is not read")]
    internal static partial void NameOutsideItsRule(ILogger logger, string name);
}