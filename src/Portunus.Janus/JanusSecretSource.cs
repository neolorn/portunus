using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

using JanusCore = Janus.Core;

namespace Portunus.Janus;

/// <summary>
/// Janus's <see cref="JanusCore.ISecretSource"/> over the client (<c>04</c> PRT-LIB-006, <c>03</c>
/// PRT-ROT-002): each member reads its field at its address, in the form Janus's type demands.
/// A secret it cannot read, or whose value is not of that form, answers Janus's
/// <c>model.startup.secretunavailable</c> naming the secret as Janus names it, a value not of
/// its form logged as <c>secrets.secret.forminvalid</c> with its address.
/// </summary>
internal sealed class JanusSecretSource(JanusConfiguration configuration, ISecretClient client, ILogger log) : JanusCore.ISecretSource
{
    private const string KeyEncryptionKeys = "keyEncryptionKeys";

    private const string FingerprintKeys = "fingerprintKeys";

    private const string MaintenanceCredential = "maintenanceCredential";

    private const string MailServerSecret = "mailServerSecret";

    private const string SocialProvider = "socialProvider.";

    public async ValueTask<JanusCore.Result<JanusCore.KeyEncryptionKeys>> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken)
    {
        (Secret? secret, SecretAddress? address) = await ReadAsync(KeyEncryptionKeys, cancellationToken).ConfigureAwait(false);

        // A key-encryption key is an AES key: 16, 24 or 32 bytes.
        return secret is not null && Decoded(secret, address!.Value, value => value.Length is 16 or 24 or 32) is { } versions
            ? JanusCore.Result.Success(new JanusCore.KeyEncryptionKeys(secret.Current, versions))
            : Unavailable<JanusCore.KeyEncryptionKeys>(KeyEncryptionKeys);
    }

    public async ValueTask<JanusCore.Result<JanusCore.FingerprintKeys>> ReadFingerprintKeysAsync(CancellationToken cancellationToken)
    {
        (Secret? secret, SecretAddress? address) = await ReadAsync(FingerprintKeys, cancellationToken).ConfigureAwait(false);

        // Janus checks a fingerprint key's length itself.
        return secret is not null && Decoded(secret, address!.Value, _ => true) is { } versions
            ? JanusCore.Result.Success(new JanusCore.FingerprintKeys(secret.Current, versions))
            : Unavailable<JanusCore.FingerprintKeys>(FingerprintKeys);
    }

    public ValueTask<JanusCore.Result<ReadOnlyMemory<byte>>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken)
    {
        return ReadTextAsync(MaintenanceCredential, cancellationToken);
    }

    public ValueTask<JanusCore.Result<ReadOnlyMemory<byte>>> ReadMailServerSecretAsync(CancellationToken cancellationToken)
    {
        return ReadTextAsync(MailServerSecret, cancellationToken);
    }

    public async ValueTask<JanusCore.Result<JanusCore.ProviderCredential>> ReadProviderCredentialAsync(string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        string name = SocialProvider + provider;
        (Secret? secret, SecretAddress? address) = await ReadAsync(name, cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            return Unavailable<JanusCore.ProviderCredential>(name);
        }

        ReadOnlyMemory<byte> value = secret.CurrentValue;
        if (value.Span is not [(byte)'{', ..])
        {
            return JanusCore.Result.Success(JanusCore.ProviderCredential.Secret(value));
        }

        try
        {
            if (SignedCredential.Read(value.Span) is JanusCore.ProviderCredential signed)
            {
                return JanusCore.Result.Success(signed);
            }

            JanusLog.FormInvalid(log, address!.Value);
            return Unavailable<JanusCore.ProviderCredential>(name);
        }
        finally
        {
            // The object's own text is not passed on: its key went into an array of its own.
            Zero(value);
        }
    }

    /// <summary>A single secret, its value's UTF-8 bytes.</summary>
    private async ValueTask<JanusCore.Result<ReadOnlyMemory<byte>>> ReadTextAsync(string name, CancellationToken cancellationToken)
    {
        (Secret? secret, SecretAddress? _) = await ReadAsync(name, cancellationToken).ConfigureAwait(false);
        return secret is null ? Unavailable<ReadOnlyMemory<byte>>(name) : JanusCore.Result.Success(secret.CurrentValue);
    }

    /// <summary>The secret <paramref name="name"/> at its address, or none where it cannot be read.</summary>
    private async ValueTask<(Secret? Secret, SecretAddress? Address)> ReadAsync(string name, CancellationToken cancellationToken)
    {
        SecretAddress address;
        try
        {
            address = configuration.Address(name);
        }
        catch (ArgumentException)
        {
            return (null, null);
        }

        Portunus.Core.Result<Secret> read = await client.ReadAsync(address, cancellationToken).ConfigureAwait(false);
        return (read.Value, address);
    }

    /// <summary>Each version decoded from base64 into a pinned array, the text zeroed; none, with the form's Warning, where one does not decode or <paramref name="usable"/> refuses it.</summary>
    private Dictionary<int, ReadOnlyMemory<byte>>? Decoded(Secret secret, SecretAddress address, Func<byte[], bool> usable)
    {
        Dictionary<int, ReadOnlyMemory<byte>> versions = [];
        try
        {
            foreach ((int number, ReadOnlyMemory<byte> text) in secret.Versions.OrderBy(version => version.Key))
            {
                byte[]? value = Base64Text.Decode(text.Span);
                if (value is null || !usable(value))
                {
                    if (value is not null)
                    {
                        CryptographicOperations.ZeroMemory(value);
                    }

                    foreach (ReadOnlyMemory<byte> decoded in versions.Values)
                    {
                        Zero(decoded);
                    }

                    JanusLog.FormInvalid(log, address);
                    return null;
                }

                versions[number] = value;
            }

            return versions;
        }
        finally
        {
            foreach (ReadOnlyMemory<byte> text in secret.Versions.Values)
            {
                Zero(text);
            }
        }
    }

    /// <summary>Janus's refusal of a secret it needs: <c>model.startup.secretunavailable</c>, <c>details.key</c> the name Janus gives it.</summary>
    private static JanusCore.Result<TValue> Unavailable<TValue>(string name)
    {
        return JanusCore.Result.Failure<TValue>(JanusCore.Error.From(JanusCore.ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(name, JanusJson.Default.String)));
    }

    private static void Zero(ReadOnlyMemory<byte> value)
    {
        if (MemoryMarshal.TryGetArray(value, out ArraySegment<byte> array))
        {
            CryptographicOperations.ZeroMemory(array);
        }
    }
}