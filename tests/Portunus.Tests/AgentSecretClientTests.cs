using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

using Xunit;

namespace Portunus.Tests;

[Trait("Category", "integration")]
public sealed class AgentSecretClientTests : IAsyncDisposable
{
    private static readonly SecretAddress Connection = SecretAddress.Parse("Themis/connection");

    private static readonly byte[] First = Recorded.Value("sample-connection-first");

    private static readonly byte[] Second = Recorded.Value("sample-connection-second");

    private static readonly byte[] Third = Recorded.Value("sample-connection-third");

    private readonly TestClock _clock = new();

    private readonly CapturedLog _log = new();

    private readonly string _socket = Scratch.NewPath(".sock");

    private FakeAgent? _agent;

    [Fact]
    public async Task PRT_LIB_004_AC1_ReadWhileTheAgentWaitsAnswersTheSecretAfterTheWaitLoggedOnce()
    {
        StartAgent(_ => _clock.Elapsed < TimeSpan.FromSeconds(10) ? Recorded.Waiting("fetching") : Recorded.Secret(1, First));
        AgentSecretClient client = Client(waitTimeout: TimeSpan.FromMinutes(1));

        Result<Secret> read = await DriveAsync(client.ReadAsync(Connection, TestContext.Current.CancellationToken));

        Assert.Equal(First, read.Value!.CurrentValue.ToArray());
        Assert.InRange(_clock.Elapsed, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12));
        CapturedLine wait = Assert.Single(_log.Lines);
        Assert.Equal(LogLevel.Warning, wait.Level);
        Assert.StartsWith("waiting for secrets", wait.Message, StringComparison.Ordinal);
        Assert.Contains("Reason=fetching", wait.Members);
    }

    [Fact]
    public async Task PRT_LIB_004_AC2_ReadWithNoSocketAnswersTheWaitTimeoutOnceItHasPassedAndNotBefore()
    {
        AgentSecretClient client = Client(waitTimeout: TimeSpan.FromSeconds(7));
        Task<Result<Secret>> read = client.ReadAsync(Connection, TestContext.Current.CancellationToken).AsTask();

        while (await _clock.NextTimerAsync(TimeSpan.FromSeconds(2), read) is TimeSpan due)
        {
            Assert.False(read.IsCompleted);
            Assert.True(_clock.Elapsed < TimeSpan.FromSeconds(7));
            _clock.Advance(due);
        }

        Failure failure = (await read).Failure!;
        Assert.Equal("secrets.agent.waittimeout", failure.Code);
        Assert.Equal("unreachable", failure.Details["reason"]);
        Assert.Equal(TimeSpan.FromSeconds(7), _clock.Elapsed);
    }

    [Fact]
    public async Task PRT_LIB_004_AC3_DeniedAddressAnswersAtOnceWithoutWaiting()
    {
        StartAgent(_ => Recorded.Failure("waiting", "secrets.secret.denied"));
        AgentSecretClient client = Client(waitTimeout: TimeSpan.FromMinutes(1));

        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.secret.denied", read.Failure!.Code);
        Assert.Equal(TimeSpan.Zero, _clock.Elapsed);
        Assert.Single(_agent!.Requests);
        Assert.DoesNotContain(_log.Lines, line => line.Message.StartsWith("waiting for secrets", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PRT_LIB_004_AC4_NoLogLineOfTheReadsHoldsAValueTheyUsed()
    {
        StartAgent(request => request.Contains("Themis/other", StringComparison.Ordinal)
            ? Recorded.Failure("ready", "secrets.secret.unknown")
            : _clock.Elapsed < TimeSpan.FromSeconds(2) ? Recorded.Waiting("fetching") : Recorded.Secret(2, First, Second));
        AgentSecretClient client = Client();

        await DriveAsync(client.ReadAsync(Connection, TestContext.Current.CancellationToken));
        await client.ReadAsync(SecretAddress.Parse("Themis/other"), TestContext.Current.CancellationToken);

        Assert.Equal(2, _log.Lines.Count);
        _log.HoldsNone(First, Second);
    }

    [Fact]
    public async Task ReadAsync_SecretOfThreeVersions_AnswersEachInArraysOfItsOwn()
    {
        StartAgent(_ => Recorded.Secret(2, First, Second, Third));
        AgentSecretClient client = Client();

        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Secret secret = read.Value!;
        Assert.Equal(2, secret.Current);
        Assert.Equal([First, Second, Third], secret.Versions.OrderBy(version => version.Key).Select(version => version.Value.ToArray()));
        Assert.Equal("""{"version":1,"kind":"read","address":"Themis/connection"}""", Assert.Single(_agent!.Requests));
        Assert.Empty(_log.Lines);
    }

    [Fact]
    public async Task ReadAsync_AddressTheSetLacks_AnswersUnknownAtOnceAndLogsIt()
    {
        StartAgent(_ => Recorded.Failure("ready", "secrets.secret.unknown"));
        AgentSecretClient client = Client();

        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.secret.unknown", read.Failure!.Code);
        CapturedLine line = Assert.Single(_log.Lines);
        Assert.Contains("Address=Themis/connection", line.Members);
        Assert.Contains("Code=secrets.secret.unknown", line.Members);
    }

    [Fact]
    public async Task ReadAsync_AgentOfAnotherProtocolVersion_AnswersTheVersionFailureAtOnce()
    {
        StartAgent(_ => """{"version":2,"state":"ready"}""");
        AgentSecretClient client = Client();

        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.request.version", read.Failure!.Code);
        Assert.Equal("2", read.Failure.Details["version"]);
        Assert.Equal(TimeSpan.Zero, _clock.Elapsed);
    }

    [Fact]
    public async Task ReadAsync_WaitTimeoutOfZero_AsksOnce()
    {
        StartAgent(_ => Recorded.Waiting("ratelimited"));
        AgentSecretClient client = Client(waitTimeout: TimeSpan.Zero);

        Result<Secret> read = await client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.agent.waittimeout", read.Failure!.Code);
        Assert.Equal("ratelimited", read.Failure.Details["reason"]);
        Assert.Single(_agent!.Requests);
    }

    [Fact]
    public async Task ReadAsync_AgentClosingWithoutAnAnswer_WaitsAsForAnAgentItCannotReach()
    {
        StartAgent(_ => _clock.Elapsed < TimeSpan.FromSeconds(4) ? null : Recorded.Secret(1, First));
        AgentSecretClient client = Client();

        Result<Secret> read = await DriveAsync(client.ReadAsync(Connection, TestContext.Current.CancellationToken));

        Assert.Equal(First, read.Value!.CurrentValue.ToArray());
        Assert.Contains("Reason=unreachable", Assert.Single(_log.Lines).Members);
    }

    [Fact]
    public async Task ReadAsync_AnswerOutsideEveryForm_WaitsAsForAnAgentItCannotReach()
    {
        StartAgent(_ => """{"version":1,"state":"ready","secret":{"current":1}}""");
        AgentSecretClient client = Client(waitTimeout: TimeSpan.FromSeconds(4));

        Result<Secret> read = await DriveAsync(client.ReadAsync(Connection, TestContext.Current.CancellationToken));

        Assert.Equal("unreachable", read.Failure!.Details["reason"]);
        Assert.Equal(3, _agent!.Requests.Count);
    }

    [Fact]
    public async Task ReadAsync_WaitOfMoreThanAMinute_LogsTheWaitAtMostEveryThirtySeconds()
    {
        StartAgent(_ => Recorded.Waiting("fetching"));
        AgentSecretClient client = Client(waitTimeout: TimeSpan.FromSeconds(70));

        Result<Secret> read = await DriveAsync(client.ReadAsync(Connection, TestContext.Current.CancellationToken));

        Assert.Equal("fetching", read.Failure!.Details["reason"]);
        string[] waits = [.. _log.Lines.Where(line => line.Message.StartsWith("waiting for secrets", StringComparison.Ordinal)).Select(line => line.Message)];
        Assert.Equal(3, waits.Length);
    }

    [Fact]
    public async Task StatusAsync_ReadyAgent_AnswersItsStatus()
    {
        StartAgent(_ => Recorded.Ready(4));
        AgentSecretClient client = Client();

        AgentStatus status = (await client.StatusAsync(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(AgentState.Ready, status.State);
        Assert.Equal(SetSource.Vault, status.Source);
        Assert.Equal(TestClock.Start.AddSeconds(12), status.LoadedAt);
        Assert.Equal(4, status.Items);
        Assert.Equal("""{"version":1,"kind":"status"}""", Assert.Single(_agent!.Requests));
    }

    [Fact]
    public async Task StatusAsync_WaitingAgent_AnswersItsReason()
    {
        StartAgent(_ => Recorded.Waiting("awaitingpush"));
        AgentSecretClient client = Client();

        AgentStatus status = (await client.StatusAsync(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(AgentState.Waiting, status.State);
        Assert.Equal(WaitReason.AwaitingPush, status.Reason);
        Assert.Equal(TestClock.Start, status.Since);
    }

    [Fact]
    public async Task StatusAsync_NoSocket_AnswersUnreachableOnce()
    {
        AgentSecretClient client = Client();

        Result<AgentStatus> status = await client.StatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("secrets.agent.unreachable", status.Failure!.Code);
        Assert.Equal(TimeSpan.Zero, _clock.Elapsed);
    }

    public async ValueTask DisposeAsync()
    {
        if (_agent is not null)
        {
            await _agent.DisposeAsync();
        }

        // No line the client logged holds a value the test used (05 PRT-OPS-005).
        _log.HoldsNone(First, Second, Third);
    }

    private void StartAgent(Func<string, string?> answer)
    {
        _agent = FakeAgent.Start(_socket, answer);
    }

    private AgentSecretClient Client(TimeSpan? waitTimeout = null)
    {
        ClientOptions options = new() { Socket = _socket, WaitTimeout = waitTimeout ?? TimeSpan.FromMinutes(5) };
        return new AgentSecretClient(options, _clock, _log);
    }

    /// <summary>Advances the clock through each pause of the read until it answers.</summary>
    private async Task<T> DriveAsync<T>(ValueTask<T> operation)
    {
        Task<T> running = operation.AsTask();
        while (await _clock.NextTimerAsync(TimeSpan.FromSeconds(2), running) is TimeSpan due)
        {
            _clock.Advance(due);
        }

        return await running;
    }
}