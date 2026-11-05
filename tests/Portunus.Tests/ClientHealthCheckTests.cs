using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace Portunus.Tests;

[Trait("Category", "integration")]
public sealed class ClientHealthCheckTests : IAsyncDisposable
{
    private readonly TestClock _clock = new();

    private readonly CapturedLog _log = new();

    private readonly string _socket = Scratch.NewPath(".sock");

    private FakeAgent? _agent;

    [Fact]
    public async Task PRT_OPS_004_AC1_ReadyAgentReadsHealthy()
    {
        _agent = FakeAgent.Start(_socket, _ => Recorded.Ready(4));

        HealthCheckResult result = await CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task PRT_OPS_004_AC1_WaitingAgentReadsDegradedWithTheReason()
    {
        _agent = FakeAgent.Start(_socket, _ => Recorded.Waiting("ratelimited"));

        HealthCheckResult result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("ratelimited", result.Data["reason"]);
    }

    [Fact]
    public async Task PRT_OPS_004_AC1_AgentItCannotReachReadsDegradedWithUnreachable()
    {
        HealthCheckResult result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("unreachable", result.Data["reason"]);
    }

    [Fact]
    public async Task CheckHealthAsync_FileSource_ReadsHealthy()
    {
        string file = Scratch.NewPath(".json");
        await File.WriteAllTextAsync(file, """{"vault":"Sample Development","items":{"Themis":{"connection":"sample-connection"}}}""", TestContext.Current.CancellationToken);
        try
        {
            using FileSecretClient client = FileSecretClient.Read(file, _clock, _log);

            HealthCheckResult result = await new ClientHealthCheck(client).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_agent is not null)
        {
            await _agent.DisposeAsync();
        }
    }

    private async Task<HealthCheckResult> CheckAsync()
    {
        ClientHealthCheck check = new(new AgentSecretClient(new ClientOptions { Socket = _socket }, _clock, _log));
        return await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
    }
}