using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Portunus.Core;

using Xunit;

namespace Portunus.Testing.Tests;

[Trait("Category", "unit")]
public sealed class FixedSecretClientTests
{
    private static readonly SecretAddress Connection = SecretAddress.Parse("Themis/connection");

    private static readonly Secret Set = new() { Current = 1, Versions = new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Encoding.UTF8.GetBytes("sample-connection") } };

    private readonly FixedSecretClient _client = new();

    [Fact]
    public async Task ReadAsync_NothingSet_AnswersUnknown()
    {
        Result<Secret> read = await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.secret.unknown", read.Failure!.Code);
    }

    [Fact]
    public async Task StatusAsync_NothingSet_AnswersReady()
    {
        AgentStatus status = (await _client.StatusAsync(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(AgentState.Ready, status.State);
        Assert.Equal(0, status.Items);
    }

    [Fact]
    public async Task ReadAsync_SecretSet_AnswersItAtAnAddressOfAnyCase()
    {
        _client.Set(Connection, Set);

        Result<Secret> read = await _client.ReadAsync(SecretAddress.Parse("themis/Connection"), TestContext.Current.CancellationToken);

        Assert.Same(Set, read.Value);
        Assert.Equal(1, (await _client.StatusAsync(TestContext.Current.CancellationToken)).Value!.Items);
    }

    [Fact]
    public async Task ReadAsync_SecretRemoved_AnswersUnknown()
    {
        _client.Set(Connection, Set);
        _client.Remove(Connection);

        Result<Secret> read = await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.secret.unknown", read.Failure!.Code);
    }

    [Fact]
    public async Task ReadAsync_AddressDenied_AnswersDeniedWhileWaitingToo()
    {
        _client.Set(Connection, Set);
        _client.Deny(Connection);
        _client.Wait(WaitReason.Fetching);

        Result<Secret> read = await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal("secrets.secret.denied", read.Failure!.Code);
    }

    [Fact]
    public async Task ReadAsync_WhileWaiting_AnswersTheWaitTimeoutWithTheReason()
    {
        _client.Set(Connection, Set);
        _client.Wait(WaitReason.RateLimited);

        Result<Secret> read = await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);
        AgentStatus status = (await _client.StatusAsync(TestContext.Current.CancellationToken)).Value!;

        Assert.Equal("secrets.agent.waittimeout", read.Failure!.Code);
        Assert.Equal("ratelimited", read.Failure.Details["reason"]);
        Assert.Equal(AgentState.Waiting, status.State);
        Assert.Equal(WaitReason.RateLimited, status.Reason);
    }

    [Fact]
    public async Task ReadAsync_Released_AnswersTheSecretAgain()
    {
        _client.Set(Connection, Set);
        _client.Wait(WaitReason.AwaitingPush);
        _client.Release();

        Result<Secret> read = await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Same(Set, read.Value);
        Assert.Equal(AgentState.Ready, (await _client.StatusAsync(TestContext.Current.CancellationToken)).Value!.State);
    }

    [Fact]
    public async Task Reads_EveryAddressAsked_InOrder()
    {
        SecretAddress keys = SecretAddress.Parse("Janus/keys");
        _client.Deny(keys);

        await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);
        await _client.ReadAsync(keys, TestContext.Current.CancellationToken);
        await _client.ReadAsync(Connection, TestContext.Current.CancellationToken);

        Assert.Equal([Connection, keys, Connection], _client.Reads);
    }
}