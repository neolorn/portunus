using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// The client's health check, <c>portunus</c> (<c>05</c> PRT-OPS-004): <c>Healthy</c> where
/// the agent is <c>ready</c>, and <c>Degraded</c> where it is <c>waiting</c> or cannot be
/// reached, its data's <c>reason</c> the agent's reason or <c>unreachable</c>; never
/// <c>Unhealthy</c>, since a running process already holds what it read. It names no secret
/// and no value.
/// </summary>
internal sealed class ClientHealthCheck(ISecretClient client) : IHealthCheck
{
    private const string Unreachable = "unreachable";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Result<AgentStatus> status = await client.StatusAsync(cancellationToken).ConfigureAwait(false);
        return status.Value switch
        {
            { State: AgentState.Ready } => HealthCheckResult.Healthy(),
            { State: AgentState.Waiting, Reason: WaitReason reason } => Degraded(Spellings.Of(reason)),
            _ => Degraded(Unreachable),
        };
    }

    private static HealthCheckResult Degraded(string reason)
    {
        return HealthCheckResult.Degraded(data: new Dictionary<string, object> { ["reason"] = reason }.ToFrozenDictionary(StringComparer.Ordinal));
    }
}