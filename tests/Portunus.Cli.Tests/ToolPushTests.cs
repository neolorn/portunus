using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>
/// <c>portunus push</c> through the fake <c>op</c>, answering as the owner's recording of the
/// vault "Sample Test" read through the 1Password app, and the fake <c>ssh</c> into an agent run
/// as root (<c>02</c> PRT-PSH-002).
/// </summary>
[Trait("Category", "linux")]
[SupportedOSPlatform("linux")]
public sealed class ToolPushTests : IAsyncDisposable
{
    private const string Recorded = "sample-test-tool";

    private readonly RunningAgent _agent = RunningAgent.Prepared(Recorded);

    [Fact]
    public async Task PRT_PSH_002_AC1_PushRunsTheCommandsWithTheirArgumentsAndEnvironmentAndTheAgentReceivesEveryField()
    {
        await _agent.StartAsync();
        Dictionary<string, string> variables = new(_agent.Environment.Variables, StringComparer.Ordinal)
        {
            ["OP_SERVICE_ACCOUNT_TOKEN"] = "set by the test",
            ["OP_CONNECT_HOST"] = "set by the test",
            ["OP_CONNECT_TOKEN"] = "set by the test",
            ["OP_SESSION_account1"] = "set by the test",
        };

        (int exit, string output, string error) = await PushAsync(new ToolEnvironment(variables));

        Assert.Equal(0, exit);
        Assert.Equal("""{"version":1,"accepted":true,"items":4}""", output.Trim());
        Assert.Empty(error);
        (string[] Arguments, string[] Environment, int ProcessId)[] calls = [.. Recording.Calls(_agent.Scenario)];
        string[][] expected =
        [
            ["--version"],
            ["item", "list", "--vault", "Sample Test", "--format", "json"],
            .. Recording.Listed(Recorded + ".list.out").Select(entry => new[] { "item", "get", entry.Item, "--vault", entry.Vault, "--format", "json", "--reveal" }),
            ["-T", "-o", "StrictHostKeyChecking=yes", "--", "owner@host", "portunus-agent", "receive", RunningAgent.Instance],
        ];
        Assert.Equal(expected, calls.Select(call => call.Arguments));
        Assert.All(calls[..^1], call => Assert.DoesNotContain(call.Environment, name => name is "OP_SERVICE_ACCOUNT_TOKEN" or "OP_CONNECT_HOST" or "OP_CONNECT_TOKEN" || name.StartsWith("OP_SESSION_", StringComparison.Ordinal)));
        Assert.Equal(Single(Recording.Value(Recorded, "Themis", "connection")), await _agent.ReadAsync("Themis/connection"));
        Assert.Equal(Single(Recording.Value(Recorded, "Hermes", "token")), await _agent.ReadAsync("Hermes/token"));
        Assert.Equal(Single(Recording.Value(Recorded, "Ceres", "password")), await _agent.ReadAsync("Ceres/password"));
        string current = Encoding.UTF8.GetString(Recording.Value(Recorded, "Janus", "keys.current"));
        string versions = string.Join(",", Enumerable.Range(1, 3).Select(version => $"\"{version}\":\"{Base64Url.EncodeToString(Recording.Value(Recorded, "Janus", $"keys.{version}"))}\""));
        Assert.Equal("{\"version\":1,\"state\":\"ready\",\"secret\":{\"current\":" + current + ",\"versions\":{" + versions + "}}}", await _agent.ReadAsync("Janus/keys"));
    }

    [Fact]
    public async Task PRT_PSH_002_AC3_PushWritesNoValueOnItsOutputOrErrorOrInAnyFile()
    {
        await _agent.StartAsync();

        (int exit, string output, string error) = await PushAsync(_agent.Environment);

        Assert.Equal(0, exit);
        byte[][] values = [.. Recording.Values(Recorded)];
        string[] forms = [.. values.Select(value => Encoding.UTF8.GetString(value)), .. values.Select(value => Base64Url.EncodeToString(value))];
        Assert.All(forms, form => Assert.DoesNotContain(form, output, StringComparison.Ordinal));
        Assert.All(forms, form => Assert.DoesNotContain(form, error, StringComparison.Ordinal));

        // The fakes' folder holds the recording of op's answers, which hold the vault's values;
        // the agent's sockets hold nothing to read.
        foreach (string file in Directory.EnumerateFiles(_agent.Folder, "*", SearchOption.AllDirectories).Where(file => !file.StartsWith(_agent.FakesFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFileName(file) != "agent.sock"))
        {
            byte[] content = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
            Assert.All(forms, form => Assert.True(content.AsSpan().IndexOf(Encoding.UTF8.GetBytes(form)) < 0, file));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _agent.DisposeAsync();
    }

    /// <summary>The agent's answer to a read of a single secret holding <paramref name="value"/>.</summary>
    private static string Single(byte[] value)
    {
        return "{\"version\":1,\"state\":\"ready\",\"secret\":{\"current\":1,\"versions\":{\"1\":\"" + Base64Url.EncodeToString(value) + "\"}}}";
    }

    private static async Task<(int Exit, string Output, string Error)> PushAsync(ToolEnvironment environment)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await ToolCommand.MainAsync(["push", "--vault", "Sample Test", "--to", "owner@host", "--instance", RunningAgent.Instance], environment, output, error, TestContext.Current.CancellationToken);
        return (exit, output.ToString(), error.ToString());
    }
}