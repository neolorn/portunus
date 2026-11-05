using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

using Themis.Core;

namespace Portunus.Themis;

/// <summary>
/// Themis's <see cref="ISecretSource"/> over the client (<c>04</c> PRT-LIB-006): each name read
/// at its address and passed as its UTF-8 bytes, the table's one name, <c>connection</c>, and any
/// name the table does not list alike, the latter logged once (P-037); every live version, in
/// ascending order, a single secret as version 1. It answers none for any failure, so Themis
/// refuses its start naming the secret where it needs it.
/// </summary>
internal sealed class ThemisSecretSource(ThemisConfiguration configuration, ISecretClient client, ILogger log) : ISecretSource
{
    private const string Denied = "secrets.secret.denied";

    /// <summary>The name Themis reads as UTF-8 text, the table's one (<c>04</c> PRT-LIB-006).</summary>
    private const string Connection = "connection";

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
            ThemisLog.NameOutsideItsRule(log, name);
            return null;
        }

        if (!string.Equals(name, Connection, StringComparison.Ordinal) && _unlisted.TryAdd(name, true))
        {
            ThemisLog.Unlisted(log, name, address);
        }

        Portunus.Core.Result<Secret> read = await client.ReadAsync(address, cancellationToken).ConfigureAwait(false);
        if (read.Value is not Secret secret)
        {
            if (read.Failure?.Code == Denied)
            {
                ThemisLog.Denied(log, name, address);
            }

            return null;
        }

        return new SecretVersions { Versions = [.. secret.Versions.OrderBy(version => version.Key).Select(version => new SecretVersion { Version = version.Key, Value = version.Value })] };
    }
}