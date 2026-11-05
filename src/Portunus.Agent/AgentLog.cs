using System;

using Microsoft.Extensions.Logging;

namespace Portunus.Agent;

/// <summary>
/// What the agent logs (<c>05</c> PRT-OPS-005): its start, each fetch attempt and its outcome,
/// the move to <c>ready</c>, each read, each connection the peer check refused, and its stop;
/// by instance, title, address, process, user identifier, state, reason, code, count and
/// <c>op</c>'s command and exit code, never a value, the token or <c>op</c>'s output.
/// </summary>
internal static partial class AgentLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "agent {Instance} started for vault {Vault}, source {Source}")]
    internal static partial void Started(ILogger logger, string instance, string vault, string source);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "fetch attempt {Attempt}: listing the vault's items")]
    internal static partial void FetchStarted(ILogger logger, int attempt);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "fetch attempt {Attempt}: {Items} items, {Reads} reads of the quota")]
    internal static partial void FetchListed(ILogger logger, int attempt, int items, int reads);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "fetch attempt {Attempt} failed: op {Command} exited {ExitCode}, reason {Reason}; next attempt in {Pause}")]
    internal static partial void FetchFailed(ILogger logger, int attempt, string command, int? exitCode, string reason, TimeSpan pause);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "fetch attempt {Attempt}: the set breaks the layout, {Code} at item {Item}, field {Field}; no further attempt")]
    internal static partial void FetchLayoutInvalid(ILogger logger, int attempt, string code, string? item, string? field);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "ready: {Items} items from {Source}")]
    internal static partial void Ready(ILogger logger, int items, string source);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "read by {Process} of {Address}: {Outcome}")]
    internal static partial void Read(ILogger logger, string process, Portunus.Core.SecretAddress address, string outcome);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "connection to {Process}'s socket refused: peer user {Peer}")]
    internal static partial void PeerRefused(ILogger logger, string process, string peer);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "agent {Instance} stopped")]
    internal static partial void Stopped(ILogger logger, string instance);
}