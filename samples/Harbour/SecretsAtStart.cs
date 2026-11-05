using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Harbour;

/// <summary>
/// Reads, at the harbour's start, its own manifest key through the client and Hermes's
/// connection through Hermes's secret source, and logs how many versions each holds, never a value;
/// it zeroes the arrays it was handed once read, and refuses the start where either is missing.
/// </summary>
internal sealed partial class SecretsAtStart(ISecretClient client, ISecretSource hermes, ILogger<SecretsAtStart> log) : IHostedService
{
    /// <summary>The harbour's own secret: the key its manifests are signed with.</summary>
    internal static readonly SecretAddress ManifestKey = new("Harbour", "manifestKey");

    /// <summary>The name Hermes reads its database connection by.</summary>
    internal const string Connection = "connection";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Portunus.Core.Result<Secret> manifest = await client.ReadAsync(ManifestKey, cancellationToken).ConfigureAwait(false);
        if (!manifest.IsSuccess)
        {
            LogRefused(ManifestKey.ToString(), manifest.Failure!.Code);
            throw new InvalidOperationException($"The harbour cannot start without {ManifestKey}.");
        }

        Secret key = manifest.Value!;
        LogManifestKey(key.Current, key.Versions.Count);

        foreach (ReadOnlyMemory<byte> value in key.Versions.Values)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsMemory(value).Span);
        }

        SecretVersions? connection = await hermes.ReadAsync(Connection, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            LogRefused(Connection, "none");
            throw new InvalidOperationException("The harbour cannot start without Hermes's connection.");
        }

        LogConnection(connection.Versions.Count);

        foreach (SecretVersion version in connection.Versions)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsMemory(version.Value).Span);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The manifest key holds {Count} version(s), the current {Current}.")]
    private partial void LogManifestKey(int current, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hermes's connection holds {Count} version(s).")]
    private partial void LogConnection(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "The secret {Secret} could not be read: {Code}.")]
    private partial void LogRefused(string secret, string code);
}