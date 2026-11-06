using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Portunus.Agent.Serving;
using Portunus.Core;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>
/// The fetch of a running <c>vault</c> agent through the fake <c>op</c>, answering as the
/// owner's recordings of the sample vaults do (<c>01</c> PRT-AGT-001, PRT-AGT-003, PRT-AGT-004,
/// PRT-AGT-005; <c>02</c> PRT-PSH-003). Time is the test's clock; the token and every pushed
/// value are made at run time.
/// </summary>
[Trait("Category", "linux")]
public sealed class AgentFetchTests : IAsyncDisposable
{
    private const string Processes = """{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection","Janus/keys"]}}""";

    private static readonly TimeSpan First = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan Max = TimeSpan.FromSeconds(40);

    private readonly AgentFiles _files = AgentFiles.OfRoot();

    private readonly CapturedLog _log = new();

    private readonly TestClock _clock = new();

    private readonly byte[] _token = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)));

    private AgentToken? _held;

    private AgentSettings? _settings;

    private IHost? _host;

    private SetHolder Holder => _host!.Services.GetRequiredService<SetHolder>();

    private string Web => Path.Combine(_files.InstanceDirectory, "web", "agent.sock");

    [Fact]
    public async Task PRT_AGT_003_AC1_VaultOfFourValidItemsIsFetchedWithOneListAndFourGetsAndTheAgentIsReady()
    {
        FakeOp op = FakeOp.Answering(_files, "sample-test");

        await StartAsync(op);
        await WaitForAsync(() => Holder.IsReady);

        string config = _settings!.OpDirectory;
        IReadOnlyList<(string Item, string Vault)> listed = Recording.Listed("sample-test.list.out");
        string[][] expected =
        [
            ["item", "list", "--vault", "Sample Test", "--format", "json", "--config", config],
            .. listed.Select(entry => new[] { "item", "get", entry.Item, "--vault", entry.Vault, "--reveal", "--format", "json", "--config", config }),
        ];
        Assert.Equal(4, listed.Count);
        Assert.Equal(expected, op.Calls());
        Assert.All(op.Environments(), names => Assert.Equal(["HOME", "OP_CACHE", "OP_SERVICE_ACCOUNT_TOKEN"], names));
        Assert.Matches("""^\{"version":1,"state":"ready","since":"[0-9T:\-]+Z","source":"vault","loadedAt":"[0-9T:\-]+Z","items":4\}$""", await StatusAsync());
        Assert.Contains(_log.Lines, line => line == "fetch attempt 1: 4 items, 7 reads of the quota");
    }

    [Fact]
    public async Task PRT_AGT_003_AC2_VaultWithABrokenItemLeavesTheAgentWaitingAsLayoutInvalidHoldingNothing()
    {
        FakeOp op = FakeOp.Answering(_files, "sample-broken");

        await StartAsync(op, ("Vault", "\"Sample Broken\""));
        await WaitForAsync(() => Reason() == WaitReason.LayoutInvalid);

        Assert.False(Holder.IsReady);
        Assert.Empty(Holder.HeldArrays());
        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"layoutinvalid",""", await StatusAsync(), StringComparison.Ordinal);
        Assert.Contains(_log.Lines, line => line == "fetch attempt 1: the set breaks the layout, secrets.layout.fieldname at item Themis, field bad label; no further attempt");
    }

    [Fact]
    public async Task PRT_AGT_003_AC3_FetchedAgentRunsNoFurtherOpWhateverTheTimeAndHoldsNoToken()
    {
        FakeOp op = FakeOp.Answering(_files, "sample-test");
        await StartAsync(op);
        await WaitForAsync(() => Holder.IsReady);
        int calls = op.Calls().Count;

        _clock.Advance(TimeSpan.FromDays(30));
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(5, calls);
        Assert.Equal(calls, op.Calls().Count);
        Assert.False(_held!.IsHeld);
        Assert.All(_token, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task PRT_AGT_003_AC4_OpPassingItsTimeoutIsKilledAndTheAttemptFailsAsUnreachable()
    {
        FakeOp op = FakeOp.AnsweringNothing(_files);
        op.Answer("sample-test", listPause: TimeSpan.FromMinutes(2));
        await StartAsync(op, ("OpTimeout", "\"00:00:05\""));
        await WaitForAsync(() => op.Calls().Count == 1);

        TimeSpan bound = await _clock.NextTimerAsync(TimeSpan.FromSeconds(5));
        _clock.Advance(bound);
        await WaitForAsync(() => Reason() == WaitReason.Unreachable);

        Assert.Equal(TimeSpan.FromSeconds(5), bound);
        Assert.False(op.IsRunning());
        Assert.Contains(_log.Lines, line => line.StartsWith("fetch attempt 1 failed: op list exited ", StringComparison.Ordinal) && line.Contains(", reason unreachable; ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PRT_AGT_004_AC1_AttemptsAfterFailuresStartAtZeroThenDoubleFromFirstWithinAFifthUpToMax()
    {
        FakeOp op = FakeOp.Answering(_files, "unreachable");
        await StartAsync(op);
        TimeSpan[] expected = [First, First * 2, First * 4, Max, Max, Max];
        List<TimeSpan> starts = [];

        foreach (TimeSpan pause in expected)
        {
            await WaitForAsync(() => op.Calls().Count == starts.Count + 1);
            starts.Add(_clock.Elapsed);
            TimeSpan next = await _clock.NextTimerAsync(Max * 1.2);
            Assert.InRange(next, Larger(pause * 0.8, First), pause * 1.2);
            _clock.Advance(next);
        }

        await WaitForAsync(() => op.Calls().Count == starts.Count + 1);
        starts.Add(_clock.Elapsed);
        Assert.Equal(TimeSpan.Zero, starts[0]);
        Assert.All(starts.Zip(starts.Skip(1)), pair => Assert.True(pair.Second - pair.First >= First));
        Assert.All(op.Calls(), call => Assert.Equal(["item", "list", "--vault", "Sample Test", "--format", "json", "--config", _settings!.OpDirectory], call));
    }

    [Fact]
    public async Task PRT_AGT_004_AC2_RateLimitMakesTheNextPauseMaxAndTheStatusRateLimited()
    {
        FakeOp op = FakeOp.Answering(_files, "rate-limited");
        await StartAsync(op);
        await WaitForAsync(() => op.Calls().Count == 1);

        TimeSpan next = await _clock.NextTimerAsync(Max * 1.2);

        Assert.InRange(next, Max * 0.8, Max * 1.2);
        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"ratelimited",""", await StatusAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_AGT_004_AC3_SetBreakingTheLayoutIsFetchedOnceAndNeverAgain()
    {
        FakeOp op = FakeOp.Answering(_files, "sample-broken");
        await StartAsync(op, ("Vault", "\"Sample Broken\""));
        await WaitForAsync(() => Reason() == WaitReason.LayoutInvalid);

        _clock.Advance(TimeSpan.FromDays(30));
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        string[][] calls = [.. op.Calls()];
        Assert.Equal(3, calls.Length);
        Assert.Single(calls, call => call[1] == "list");
    }

    [Fact]
    public async Task PRT_AGT_005_AC1_StatusAnswersTheFailuresReasonBetweenAttemptsFetchingDuringOneAndReadyAfter()
    {
        FakeOp op = FakeOp.Answering(_files, "refused");
        await StartAsync(op);
        await WaitForAsync(() => Reason() == WaitReason.Refused);
        TimeSpan pause = await _clock.NextTimerAsync(First * 1.2);
        string between = await StatusAsync();

        op.Answer("sample-test", listPause: TimeSpan.FromSeconds(5));
        _clock.Advance(pause);
        await WaitForAsync(() => op.Calls().Count == 2);
        string during = await StatusAsync();
        await WaitForAsync(() => Holder.IsReady);
        string after = await StatusAsync();

        Assert.Equal($$"""{"version":1,"state":"waiting","reason":"refused","since":"{{Instant(TimeSpan.Zero)}}"}""", between);
        Assert.Equal($$"""{"version":1,"state":"waiting","reason":"fetching","since":"{{Instant(pause)}}"}""", during);
        Assert.Equal($$"""{"version":1,"state":"ready","since":"{{Instant(pause)}}","source":"vault","loadedAt":"{{Instant(pause)}}","items":4}""", after);
    }

    [Fact]
    public async Task PRT_PSH_003_AC4_PushIntoAVaultAgentWaitingBetweenAttemptsEndsItsAttempts()
    {
        FakeOp op = FakeOp.Answering(_files, "unreachable");
        await StartAsync(op);
        await WaitForAsync(() => op.Calls().Count == 1);
        TimeSpan pause = await _clock.NextTimerAsync(First * 1.2);

        (int exit, string output) = await ReceiveAsync(Push(Text(24), replace: false));
        _clock.Advance(pause);
        _clock.Advance(TimeSpan.FromDays(30));
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal("""{"version":1,"accepted":true,"items":3}""", output.Trim());
        Assert.Single(op.Calls());
        Assert.False(_held!.IsHeld);
        Assert.Contains("\"source\":\"push\"", await StatusAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_AGT_001_AC4_FetchPushAndReadsLeaveNoValueInAnyFileOrLogLine()
    {
        FakeOp op = FakeOp.Answering(_files, "sample-test");
        await StartAsync(op);
        await WaitForAsync(() => Holder.IsReady);
        string fetched = await ReadAsync("Themis/connection");
        await ReadAsync("Janus/keys");
        byte[] pushed = Text(24);

        (int exit, string _) = await ReceiveAsync(Push(pushed, replace: true));
        string read = await ReadAsync("Themis/connection");

        Assert.Contains(Base64Url.EncodeToString(Recording.Value("sample-test", "Themis", "connection")), fetched, StringComparison.Ordinal);
        Assert.Equal(0, exit);
        Assert.Contains(Base64Url.EncodeToString(pushed), read, StringComparison.Ordinal);
        byte[][] used = [.. Recording.Values("sample-test"), pushed];
        byte[][] forms = [.. used, .. used.Select(value => Encoding.ASCII.GetBytes(Base64Url.EncodeToString(value)))];

        // The fake op's folder holds its recording of op's answers, which hold the vault's values;
        // a socket holds nothing to read.
        foreach (string file in Directory.EnumerateFiles(_files.Folder, "*", SearchOption.AllDirectories).Where(file => !file.StartsWith(op.Folder + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Native.Ownership(file)?.Kind == FileKind.File))
        {
            byte[] content = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
            Assert.All(forms, form => Assert.True(content.AsSpan().IndexOf(form) < 0, file));
        }

        Assert.All(_log.Lines, line => Assert.All(forms, form => Assert.DoesNotContain(Encoding.UTF8.GetString(form), line, StringComparison.Ordinal)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
        }

        _files.Dispose();
        _log.Dispose();
    }

    private static TimeSpan Larger(TimeSpan one, TimeSpan other)
    {
        return one > other ? one : other;
    }

    /// <summary>The instant <paramref name="elapsed"/> after the test clock's start, as the agent writes it.</summary>
    private static string Instant(TimeSpan elapsed)
    {
        return (TestClock.Start + elapsed).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>Text of <paramref name="length"/> random lower-case letters, made at run time.</summary>
    private static byte[] Text(int length)
    {
        byte[] text = new byte[length];
        for (int index = 0; index < text.Length; index++)
        {
            text[index] = (byte)RandomNumberGenerator.GetInt32('a', 'z' + 1);
        }

        return text;
    }

    /// <summary>A push of three items, <c>Themis/connection</c> holding <paramref name="connection"/> and the others values made at run time.</summary>
    private static byte[] Push(byte[] connection, bool replace)
    {
        string[] keys = [Base64Url.EncodeToString(Text(32)), Base64Url.EncodeToString(Text(32))];
        return Encoding.UTF8.GetBytes(
            $$"""{"version":1,"kind":"push","vault":"Sample Test","replace":{{(replace ? "true" : "false")}},"items":[""" +
            $$"""{"title":"Themis","fields":[{"label":"connection","value":"{{Base64Url.EncodeToString(connection)}}"}]},""" +
            $$"""{"title":"Janus","fields":[{"label":"keys.1","value":"{{keys[0]}}"},{"label":"keys.2","value":"{{keys[1]}}"},{"label":"keys.current","value":"{{Base64Url.EncodeToString("1"u8)}}"}]},""" +
            $$"""{"title":"Hermes","fields":[{"label":"token","value":"{{Base64Url.EncodeToString(Text(16))}}"}]}]}""");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The agent did not get there within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Starts a <c>vault</c> agent whose <c>op</c> is <paramref name="op"/>'s launcher, the pauses
    /// <see cref="First"/> and <see cref="Max"/>, an <c>op</c> bound of 5 minutes, the test's clock
    /// and the token, as its start leaves it: its directory and <c>op</c>'s made (<c>01</c>
    /// PRT-AGT-002). Each member of <paramref name="members"/> replaces one of the configuration.
    /// </summary>
    private async Task StartAsync(FakeOp op, params (string Name, string? Json)[] members)
    {
        _files.WriteConfiguration(
        [
            ("Source", "\"vault\""),
            ("OpPath", $"\"{op.Launcher}\""),
            ("TokenFile", $"\"{Path.Combine(_files.Folder, "token")}\""),
            ("OpTimeout", "\"00:05:00\""),
            ("Retry", """{"First":"00:00:05","Max":"00:00:40"}"""),
            ("Processes", Processes),
            .. members,
        ]);
        _settings = AgentConfiguration.Read(_files.File, AgentFiles.Instance).Value!;
        SocketDirectories.PrepareInstance(_settings.InstanceDirectory);
        Directory.CreateDirectory(_settings.OpDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _held = new AgentToken(_token);
        _host = AgentCommand.BuildHost(_settings, _held, services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            services.AddSingleton<ILoggerProvider>(_log);
        });
        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    private WaitReason? Reason()
    {
        using SetHolder.Lease lease = Holder.Acquire();
        return lease.State == AgentState.Waiting ? lease.Reason : null;
    }

    private async Task<string> StatusAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        await AgentCommand.MainAsync(["status", AgentFiles.Instance], _files.Paths, Stream.Null, output, error);
        return output.ToString().Trim();
    }

    private async Task<(int Exit, string Output)> ReceiveAsync(byte[] payload)
    {
        using MemoryStream input = new(payload);
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await AgentCommand.MainAsync(["receive", AgentFiles.Instance], _files.Paths, input, output, error);
        return (exit, output.ToString());
    }

    private Task<string> ReadAsync(string address)
    {
        return Harness.ClientAsync(_files, Web, Harness.Request($$"""{"version":1,"kind":"read","address":"{{address}}"}"""));
    }
}