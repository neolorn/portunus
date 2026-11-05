using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>
/// A push carried by <c>portunus-agent receive</c> into a running agent, and the reads that
/// follow (<c>02</c> PRT-PSH-003, PRT-PSH-005; <c>01</c> PRT-AGT-001, PRT-AGT-005, PRT-AGT-007,
/// PRT-AGT-008). Every value is made at run time.
/// </summary>
[Trait("Category", "linux")]
public sealed class AgentPushTests : IAsyncDisposable
{
    private const string Processes = """{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection","Janus/keys"]},"worker":{"Uid":4245,"Gid":4246,"Secrets":["Hermes/token"]}}""";

    private readonly AgentFiles _files = AgentFiles.OfRoot();

    private readonly CapturedLog _log = new();

    private IHost? _host;

    private string Web => Path.Combine(_files.InstanceDirectory, "web", "agent.sock");

    private string PushSocket => Path.Combine(_files.InstanceDirectory, "push", "agent.sock");

    [Fact]
    public async Task PRT_PSH_003_AC1_PushIntoAWaitingAgentMakesItReadyFromPushAndAReadAnswersIt()
    {
        await StartAsync();
        Vault vault = Vault.Made();

        (int exit, string output, string error) = await ReceiveAsync(vault.Push());
        string answer = await Harness.ClientAsync(_files, Web, Harness.Request("""{"version":1,"kind":"read","address":"Themis/connection"}"""));

        Assert.Equal(0, exit);
        Assert.Equal("""{"version":1,"accepted":true,"items":3}""", output.Trim());
        Assert.Empty(error);
        Assert.Equal($$$$"""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"{{{{vault.Connection}}}}"}}}""", answer);
        Assert.StartsWith("""{"version":1,"state":"ready","since":""", await StatusAsync(), StringComparison.Ordinal);
        Assert.Contains("\"source\":\"push\"", await StatusAsync(), StringComparison.Ordinal);
        Assert.Contains(_log.Lines, line => line == "push from peer user 0: accepted, 3 items");
    }

    [Fact]
    public async Task PRT_AGT_005_AC1_StatusAfterAPushAnswersReadyWithItsSourceInstantAndItemCount()
    {
        await StartAsync();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await ReceiveAsync(Vault.Made().Push());
        string status = await StatusAsync();

        Assert.Matches("""^\{"version":1,"state":"ready","since":"[0-9T:\-]+Z","source":"push","loadedAt":"[0-9T:\-]+Z","items":3\}$""", status);
        string loadedAt = status.Split("\"loadedAt\":\"")[1].Split('"')[0];
        Assert.InRange(DateTimeOffset.Parse(loadedAt, System.Globalization.CultureInfo.InvariantCulture), before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task PRT_AGT_005_AC2_StatusOfAReadyAgentExitsZeroWithItsAnswer()
    {
        await StartAsync();
        await ReceiveAsync(Vault.Made().Push());
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = await AgentCommand.MainAsync(["status", AgentFiles.Instance], _files.Paths, Stream.Null, output, error);

        Assert.Equal(0, exit);
        Assert.StartsWith("""{"version":1,"state":"ready",""", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task PRT_AGT_008_AC2_DeclaredReadWhileReadyAnswersTheValueOfASingleSecret()
    {
        await StartAsync();
        Vault vault = Vault.Made();
        await ReceiveAsync(vault.Push());

        string answer = await Harness.ClientAsync(_files, Web, Harness.Request("""{"version":1,"kind":"read","address":"Themis/connection"}"""));

        Assert.Equal($$$$"""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"{{{{vault.Connection}}}}"}}}""", answer);
        Assert.Contains(_log.Lines, line => line == "read by web of Themis/connection: answered");
    }

    [Fact]
    public async Task PRT_AGT_008_AC2_DeclaredReadWhileReadyAnswersEachVersionOfASetAndItsCurrentOne()
    {
        await StartAsync();
        Vault vault = Vault.Made();
        await ReceiveAsync(vault.Push());

        string answer = await Harness.ClientAsync(_files, Web, Harness.Request("""{"version":1,"kind":"read","address":"Janus/keys"}"""));

        Assert.Equal($$$$"""{"version":1,"state":"ready","secret":{"current":2,"versions":{"1":"{{{{vault.Keys[0]}}}}","2":"{{{{vault.Keys[1]}}}}","3":"{{{{vault.Keys[2]}}}}"}}}""", answer);
    }

    [Theory]
    [InlineData("Janus/keys")]
    [InlineData("Ceres/missing")]
    public async Task PRT_AGT_007_AC2_UndeclaredAddressIsDeniedWhileReadyWhetherOrNotTheSetHoldsIt(string address)
    {
        await StartAsync();
        await ReceiveAsync(Vault.Made().Push());

        string answer = await Harness.ClientAsync(_files, Path.Combine(_files.InstanceDirectory, "worker", "agent.sock"), Harness.Request($$"""{"version":1,"kind":"read","address":"{{address}}"}"""), user: 4245, group: 4246);

        Assert.Equal("""{"version":1,"state":"ready","failure":{"code":"secrets.secret.denied","details":{}}}""", answer);
    }

    [Fact]
    public async Task PRT_PSH_003_AC2_PushIntoAReadyAgentWithoutReplaceIsRefusedAndChangesNothing()
    {
        await StartAsync();
        Vault first = Vault.Made();
        await ReceiveAsync(first.Push());

        (int exit, string output, string _) = await ReceiveAsync(Vault.Made().Push());
        string answer = await Harness.ClientAsync(_files, Web, Harness.Request("""{"version":1,"kind":"read","address":"Themis/connection"}"""));

        Assert.Equal(1, exit);
        Assert.Equal("""{"version":1,"accepted":false,"failure":{"code":"secrets.push.ready","details":{}}}""", output.Trim());
        Assert.Contains(first.Connection, answer, StringComparison.Ordinal);
        Assert.Contains(_log.Lines, line => line == "push from peer user 0: secrets.push.ready, 3 items");
    }

    [Fact]
    public async Task PRT_PSH_003_AC2_PushIntoAReadyAgentWithReplaceReplacesTheSet()
    {
        await StartAsync();
        await ReceiveAsync(Vault.Made().Push());
        Vault second = Vault.Made();

        (int exit, string output, string _) = await ReceiveAsync(second.Push(replace: true));
        string answer = await Harness.ClientAsync(_files, Web, Harness.Request("""{"version":1,"kind":"read","address":"Themis/connection"}"""));

        Assert.Equal(0, exit);
        Assert.Equal("""{"version":1,"accepted":true,"items":3}""", output.Trim());
        Assert.Equal($$$$"""{"version":1,"state":"ready","secret":{"current":1,"versions":{"1":"{{{{second.Connection}}}}"}}}""", answer);
    }

    [Fact]
    public async Task PRT_AGT_001_AC3_SetBeforeAReplaceAndSetAtTheStopHoldZeros()
    {
        await StartAsync();
        SetHolder holder = _host!.Services.GetRequiredService<SetHolder>();
        await ReceiveAsync(Vault.Made().Push());
        byte[][] replaced = [.. holder.HeldArrays()];

        await ReceiveAsync(Vault.Made().Push(replace: true));
        byte[][] stopped = [.. holder.HeldArrays()];
        await StopAsync();

        Assert.NotEmpty(replaced);
        Assert.All(replaced, array => Assert.All(array, value => Assert.Equal(0, value)));
        Assert.NotEmpty(stopped);
        Assert.All(stopped, array => Assert.All(array, value => Assert.Equal(0, value)));
    }

    [Fact]
    public async Task PRT_PSH_003_AC3_PushOfAnotherVaultIsRefusedWithWrongVault()
    {
        await StartAsync();

        (int exit, string output, string _) = await ReceiveAsync(Vault.Made().Push(vault: "Sample Staging"));

        Assert.Equal(1, exit);
        Assert.Equal("""{"version":1,"accepted":false,"failure":{"code":"secrets.push.wrongvault","details":{}}}""", output.Trim());
        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"awaitingpush",""", await StatusAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Take_VaultTitleOfAnotherCase_IsTheAgentsVault()
    {
        await StartAsync();

        (int exit, string _, string _) = await ReceiveAsync(Vault.Made().Push(vault: "SAMPLE test"));

        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task Take_SetBreakingTheLayout_IsRefusedNamingItsFirstFindingsItemAndField()
    {
        await StartAsync();
        string value = Base64Url.EncodeToString(Text(8));

        (int exit, string output, string _) = await ReceiveAsync(Encoding.UTF8.GetBytes($$"""{"version":1,"kind":"push","vault":"Sample Test","replace":false,"items":[{"title":"Themis","fields":[{"label":"connection","value":"{{value}}"},{"label":"CONNECTION","value":"{{value}}"}]}]}"""));

        Assert.Equal(1, exit);
        Assert.Equal("""{"version":1,"accepted":false,"failure":{"code":"secrets.push.layoutinvalid","details":{"field":"connection","item":"Themis"}}}""", output.Trim());
        Assert.StartsWith("""{"version":1,"state":"waiting",""", await StatusAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_PSH_003_AC5_PayloadOverTwoMebibytesIsRefusedByReceiveAndTheAgentReceivesNothing()
    {
        await StartAsync();
        byte[] payload = new byte[(2 * 1024 * 1024) + 1];
        Array.Fill(payload, (byte)' ');

        (int exit, string output, string error) = await ReceiveAsync(payload);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Equal("""{"code":"secrets.push.toolarge","details":{}}""", error.Trim());
        Assert.DoesNotContain(_log.Lines, line => line.StartsWith("push from", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Receive_AgentNotRunning_IsRefusedWithUnreachable()
    {
        _files.WriteConfiguration(("Processes", Processes));

        (int exit, string output, string error) = await ReceiveAsync(Vault.Made().Push());

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Equal("""{"code":"secrets.push.unreachable","details":{}}""", error.Trim());
    }

    [Fact]
    public async Task PRT_PSH_005_AC1_PushModeAgentWithNoTokenFileRunsNoOpWaitsAndIsReadyAfterAPush()
    {
        FakeOp op = FakeOp.AnsweringNothing(_files);
        string missing = Path.Combine(_files.Folder, "missing");
        _files.WriteConfiguration(("Processes", Processes), ("TokenFile", $"\"{missing}\""), ("OpPath", $"\"{op.Launcher}\""));
        using Process agent = Harness.Start(Harness.Folder, _files.Configurations, _files.Swaps, "run", AgentFiles.Instance);
        try
        {
            await WaitForAsync(() => File.Exists(PushSocket) || agent.HasExited);
            (int waiting, string waitingStatus) = await HarnessCommandAsync([], "status");

            (int received, string accepted) = await HarnessCommandAsync(Vault.Made().Push(), "receive");
            (int ready, string _) = await HarnessCommandAsync([], "status");

            Assert.Equal(3, waiting);
            Assert.StartsWith("""{"version":1,"state":"waiting","reason":"awaitingpush",""", waitingStatus, StringComparison.Ordinal);
            Assert.Equal(0, received);
            Assert.Equal("""{"version":1,"accepted":true,"items":3}""", accepted.Trim());
            Assert.Equal(0, ready);
            Assert.Empty(op.Calls());
        }
        finally
        {
            agent.Kill(entireProcessTree: true);
            await agent.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _files.Dispose();
        _log.Dispose();
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The agent did not get there within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    private async Task StartAsync()
    {
        _files.WriteConfiguration(("Processes", Processes));
        AgentSettings settings = AgentConfiguration.Read(_files.File, AgentFiles.Instance).Value!;
        _host = AgentCommand.BuildHost(settings, new AgentToken(null), services => services.AddSingleton<ILoggerProvider>(_log));
        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    private async Task StopAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
            _host = null;
        }
    }

    /// <summary>Runs <c>receive</c> with <paramref name="payload"/> on its standard input.</summary>
    private async Task<(int Exit, string Output, string Error)> ReceiveAsync(byte[] payload)
    {
        using MemoryStream input = new(payload);
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await AgentCommand.MainAsync(["receive", AgentFiles.Instance], _files.Paths, input, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    private async Task<string> StatusAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        await AgentCommand.MainAsync(["status", AgentFiles.Instance], _files.Paths, Stream.Null, output, error);
        return output.ToString().Trim();
    }

    /// <summary>Runs the agent's <paramref name="command"/> through the harness, as its own process, with <paramref name="input"/> on its standard input.</summary>
    private async Task<(int Exit, string Output)> HarnessCommandAsync(byte[] input, string command)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { Path.Combine(Harness.Folder, "Portunus.Agent.Harness.dll"), _files.Configurations, _files.Swaps, command, AgentFiles.Instance })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        await process.StandardInput.BaseStream.WriteAsync(input, TestContext.Current.CancellationToken);
        process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, output);
    }

    /// <summary>A vault of three items made at run time: a single secret, a set of three versions whose current one is 2, and a third item.</summary>
    private sealed class Vault
    {
        private readonly byte[] _connection = Text(24);

        private readonly byte[][] _keys = [Text(32), Text(32), Text(32)];

        private readonly byte[] _token = Text(16);

        /// <summary>The single secret <c>Themis/connection</c>, in base64url.</summary>
        internal string Connection => Base64Url.EncodeToString(_connection);

        /// <summary>The versions of <c>Janus/keys</c>, in base64url.</summary>
        internal IReadOnlyList<string> Keys => [.. _keys.Select(key => Base64Url.EncodeToString(key))];

        internal static Vault Made()
        {
            return new Vault();
        }

        /// <summary>The push request of the vault (<c>07</c> section 6).</summary>
        internal byte[] Push(bool replace = false, string vault = "Sample Test")
        {
            string current = Base64Url.EncodeToString("2"u8);
            string token = Base64Url.EncodeToString(_token);
            return Encoding.UTF8.GetBytes(
                $$"""{"version":1,"kind":"push","vault":"{{vault}}","replace":{{(replace ? "true" : "false")}},"items":[""" +
                $$"""{"title":"Themis","fields":[{"label":"connection","value":"{{Connection}}"}]},""" +
                $$"""{"title":"Janus","fields":[{"label":"keys.1","value":"{{Keys[0]}}"},{"label":"keys.2","value":"{{Keys[1]}}"},{"label":"keys.3","value":"{{Keys[2]}}"},{"label":"keys.current","value":"{{current}}"}]},""" +
                $$"""{"title":"Hermes","fields":[{"label":"token","value":"{{token}}"}]}]}""");
        }
    }
}