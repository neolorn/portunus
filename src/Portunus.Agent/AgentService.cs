using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Portunus.Agent.Fetch;
using Portunus.Agent.Serving;
using Portunus.Core;

namespace Portunus.Agent;

/// <summary>
/// The running agent (<c>01</c>): it listens on its sockets, and in <c>vault</c> mode fetches
/// once, trying again after growing pauses until a fetch gives it a set, a set breaks the
/// layout, or it stops (PRT-AGT-003, PRT-AGT-004). On its stop it lets the reads under way
/// finish, removes its sockets, and zeroes its set and its token (PRT-AGT-009).
/// </summary>
internal sealed class AgentService(AgentSettings settings, AgentToken token, SetHolder holder, AgentServer server, Fetcher fetcher, RetryPolicy retries, TimeProvider time, ILogger<AgentService> log) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    private Task _fetching = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        AgentLog.Started(log, settings.Instance, settings.Options.Vault, settings.Options.Source == AgentSource.Vault ? "vault" : "push");
        if (settings.Options.Source == AgentSource.Push)
        {
            holder.Wait(WaitReason.AwaitingPush);
        }

        server.Start();
        if (settings.Options.Source == AgentSource.Vault)
        {
            CancellationToken stopping = _stopping.Token;
            _fetching = FetchAsync(stopping);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _fetching.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The fetch was stopped with the agent.
        }

        await server.StopAsync().ConfigureAwait(false);
        holder.Dispose();
        token.Drop();
        AgentLog.Stopped(log, settings.Instance);
    }

    public void Dispose()
    {
        _stopping.Dispose();
    }

    private async Task FetchAsync(CancellationToken stopping)
    {
        for (int attempt = 1; ; attempt++)
        {
            holder.Wait(WaitReason.Fetching);
            FetchOutcome outcome = await fetcher.FetchAsync(attempt, token.Value, stopping).ConfigureAwait(false);
            if (outcome is { Set: VaultSet set, Values: PinnedValues values })
            {
                holder.Fill(set, values, SetSource.Vault);
                token.Drop();
                AgentLog.Ready(log, set.Items.Count, "vault");
                return;
            }

            if (outcome.Layout is LayoutFinding error)
            {
                // The vault will not mend itself, and another try would only spend the quota.
                holder.Wait(WaitReason.LayoutInvalid);
                AgentLog.FetchLayoutInvalid(log, attempt, error.Code, error.Item, error.Field);
                return;
            }

            FailureClass failure = outcome.Failure?.Failure ?? FailureClass.Unreachable;
            WaitReason reason = failure switch
            {
                FailureClass.RateLimited => WaitReason.RateLimited,
                FailureClass.Refused => WaitReason.Refused,
                _ => WaitReason.Unreachable,
            };
            holder.Wait(reason);
            TimeSpan pause = retries.Next(failure);
            AgentLog.FetchFailed(log, attempt, outcome.Command ?? "list", outcome.Failure?.ExitCode, Spellings.Of(reason), pause);
            await Task.Delay(pause, time, stopping).ConfigureAwait(false);
        }
    }
}

/// <summary>The service account's token while the agent holds it, a pinned array zeroed once dropped (<c>01</c> PRT-AGT-003).</summary>
internal sealed class AgentToken(byte[]? token)
{
    private byte[]? _token = token;

    /// <summary>The token, or nothing once dropped or in <c>push</c> mode.</summary>
    internal ReadOnlyMemory<byte> Value => _token;

    /// <summary>Whether the agent still holds the token.</summary>
    internal bool IsHeld => _token is not null;

    /// <summary>Zeroes and drops the token.</summary>
    internal void Drop()
    {
        if (Interlocked.Exchange(ref _token, null) is byte[] held)
        {
            CryptographicOperations.ZeroMemory(held);
        }
    }
}