using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Portunus.Core;

namespace Portunus.Testing;

/// <summary>
/// A client a host's tests set (<c>04</c> PRT-LIB-008), so a test runs a host and its
/// siblings with no agent: it is <see cref="AgentState.Ready"/> and answers
/// <c>secrets.secret.unknown</c> for each address until one is set, and keeps every address
/// asked. It answers a set secret as it was set, its arrays the test's own.
/// </summary>
public sealed class FixedSecretClient : ISecretClient
{
    private readonly Lock _lock = new();

    private readonly Dictionary<SecretAddress, Secret?> _addresses = [];

    private readonly List<SecretAddress> _reads = [];

    private WaitReason? _waiting;

    private DateTimeOffset _since = TimeProvider.System.GetUtcNow();

    /// <summary>Every address asked through <see cref="ReadAsync"/>, in order.</summary>
    public IReadOnlyList<SecretAddress> Reads
    {
        get
        {
            lock (_lock)
            {
                return [.. _reads];
            }
        }
    }

    /// <summary>Answers <paramref name="secret"/> for <paramref name="address"/> from now on.</summary>
    /// <param name="address">The address.</param>
    /// <param name="secret">The secret it answers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="secret"/> is null.</exception>
    public void Set(SecretAddress address, Secret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        lock (_lock)
        {
            _addresses[address] = secret;
        }
    }

    /// <summary>Answers <c>secrets.secret.unknown</c> for <paramref name="address"/> from now on, as before it was set or denied.</summary>
    /// <param name="address">The address.</param>
    public void Remove(SecretAddress address)
    {
        lock (_lock)
        {
            _addresses.Remove(address);
        }
    }

    /// <summary>Answers <c>secrets.secret.denied</c> for <paramref name="address"/> from now on, in either state, as the agent answers an address its declaration does not name.</summary>
    /// <param name="address">The address.</param>
    public void Deny(SecretAddress address)
    {
        lock (_lock)
        {
            _addresses[address] = null;
        }
    }

    /// <summary>
    /// Waits for <paramref name="reason"/> from now on: a read of an address not denied answers
    /// <c>secrets.agent.waittimeout</c> with that reason, and <see cref="StatusAsync"/>
    /// <see cref="AgentState.Waiting"/>.
    /// </summary>
    /// <param name="reason">Why the client waits.</param>
    public void Wait(WaitReason reason)
    {
        lock (_lock)
        {
            _waiting = reason;
            _since = TimeProvider.System.GetUtcNow();
        }
    }

    /// <summary>Is <see cref="AgentState.Ready"/> again from now on, answering what is set.</summary>
    public void Release()
    {
        lock (_lock)
        {
            _waiting = null;
            _since = TimeProvider.System.GetUtcNow();
        }
    }

    /// <summary>
    /// Answers what is set at <paramref name="address"/>: <c>secrets.secret.denied</c> where it
    /// is denied; while waiting, <c>secrets.agent.waittimeout</c> with the reason; else the
    /// secret set, or <c>secrets.secret.unknown</c>.
    /// </summary>
    /// <param name="address">The address to read.</param>
    /// <param name="cancellationToken">Unused; the answer comes at once.</param>
    /// <returns>The secret, or the failure above.</returns>
    public ValueTask<Result<Secret>> ReadAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _reads.Add(address);
            bool held = _addresses.TryGetValue(address, out Secret? secret);
            Result<Secret> answer = (held, secret, _waiting) switch
            {
                (true, null, _) => Result.Fail<Secret>(new Failure { Code = Codes.SecretDenied }),
                (_, _, WaitReason reason) => Result.Fail<Secret>(new Failure
                {
                    Code = Codes.AgentWaitTimeout,
                    Details = new Dictionary<string, string> { ["reason"] = Spellings.Of(reason) }.ToFrozenDictionary(StringComparer.Ordinal),
                }),
                (true, Secret found, null) => Result.Success(found),
                _ => Result.Fail<Secret>(new Failure { Code = Codes.SecretUnknown }),
            };
            return ValueTask.FromResult(answer);
        }
    }

    /// <summary>
    /// Answers <see cref="AgentState.Waiting"/> with the reason while waiting, else
    /// <see cref="AgentState.Ready"/> with the source <see cref="SetSource.Vault"/> and the
    /// number of items a secret is set in.
    /// </summary>
    /// <param name="cancellationToken">Unused; the answer comes at once.</param>
    /// <returns>The status.</returns>
    public ValueTask<Result<AgentStatus>> StatusAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            AgentStatus status = _waiting is WaitReason reason
                ? new AgentStatus { State = AgentState.Waiting, Reason = reason, Since = _since, Items = 0 }
                : new AgentStatus
                {
                    State = AgentState.Ready,
                    Since = _since,
                    Source = SetSource.Vault,
                    LoadedAt = _since,
                    Items = _addresses.Where(address => address.Value is not null).Select(address => address.Key.Item).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                };
            return ValueTask.FromResult(Result.Success(status));
        }
    }
}