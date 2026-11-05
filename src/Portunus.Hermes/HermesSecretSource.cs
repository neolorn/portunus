using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus.Hermes;

/// <summary>
/// Hermes's <see cref="ISecretSource"/> over the client (<c>04</c> PRT-LIB-006, <c>03</c>
/// PRT-ROT-002): each name read at its address, a name of the table's base64 column decoded,
/// one of its text column or one the table does not list (P-037) passed as its UTF-8 bytes;
/// every live version, in ascending order, a single secret as version 1. It answers none for
/// any failure, so Hermes refuses its start naming the secret where it needs it.
/// </summary>
internal sealed class HermesSecretSource(HermesConfiguration configuration, ISecretClient client, ILogger log) : ISecretSource
{
    private const string Denied = "secrets.secret.denied";

    /// <summary>The names Hermes reads as base64 (<c>04</c> PRT-LIB-006).</summary>
    private static readonly FrozenSet<string> Base64Names = FrozenSet.Create(StringComparer.Ordinal, "contentKeys", "hashKeys", "tokenKeys", "smsReportKey", "pushVapidKeys");

    /// <summary>The names Hermes reads as UTF-8 text (<c>04</c> PRT-LIB-006).</summary>
    private static readonly FrozenSet<string> TextNames = FrozenSet.Create(StringComparer.Ordinal, "connection", "listenConnection", "emailSubmission", "smsCequens");

    /// <summary>The names the table does not list that were read already, each logged once.</summary>
    private readonly ConcurrentDictionary<string, bool> _unlisted = new(StringComparer.Ordinal);

    public async ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        SecretAddress address;
        try
        {
            address = configuration.Address(name);
        }
        catch (ArgumentException)
        {
            HermesLog.NameOutsideItsRule(log, name);
            return null;
        }

        bool base64 = Base64Names.Contains(name);
        if (!base64 && !TextNames.Contains(name) && _unlisted.TryAdd(name, true))
        {
            HermesLog.Unlisted(log, name, address);
        }

        Portunus.Core.Result<Secret> read = await client.ReadAsync(address, cancellationToken).ConfigureAwait(false);
        if (read.Value is not Secret secret)
        {
            if (read.Failure?.Code == Denied)
            {
                HermesLog.Denied(log, name, address);
            }

            return null;
        }

        return base64 ? Decoded(secret, address) : Passed(secret);
    }

    /// <summary>Each version's text as it is.</summary>
    private static SecretVersions Passed(Secret secret)
    {
        return new SecretVersions { Versions = [.. secret.Versions.OrderBy(version => version.Key).Select(version => new SecretVersion { Version = version.Key, Value = version.Value })] };
    }

    /// <summary>Each version decoded from base64 into a pinned array, its text zeroed; none, with the form's Warning, where one does not decode.</summary>
    private SecretVersions? Decoded(Secret secret, SecretAddress address)
    {
        List<SecretVersion> versions = [];
        try
        {
            foreach ((int number, ReadOnlyMemory<byte> text) in secret.Versions.OrderBy(version => version.Key))
            {
                if (Base64Text.Decode(text.Span) is not byte[] value)
                {
                    foreach (SecretVersion decoded in versions)
                    {
                        Zero(decoded.Value);
                    }

                    HermesLog.FormInvalid(log, address);
                    return null;
                }

                versions.Add(new SecretVersion { Version = number, Value = value });
            }

            return new SecretVersions { Versions = versions };
        }
        finally
        {
            foreach (ReadOnlyMemory<byte> text in secret.Versions.Values)
            {
                Zero(text);
            }
        }
    }

    private static void Zero(ReadOnlyMemory<byte> value)
    {
        if (MemoryMarshal.TryGetArray(value, out ArraySegment<byte> array))
        {
            CryptographicOperations.ZeroMemory(array);
        }
    }
}