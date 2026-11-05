using System;
using System.Buffers.Text;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary><c>portunus status</c> through the fake <c>ssh</c> into an agent run as root (<c>02</c> PRT-PSH-004).</summary>
[Trait("Category", "linux")]
[SupportedOSPlatform("linux")]
public sealed class ToolStatusTests : IAsyncDisposable
{
    private readonly RunningAgent _agent = RunningAgent.Prepared();

    [Fact]
    public async Task PRT_PSH_004_AC2_StatusOfAWaitingAgentExitsThreeWithItsAnswer()
    {
        await _agent.StartAsync();

        (int exit, string output, string error) = await StatusAsync();

        Assert.Equal(3, exit);
        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"awaitingpush","since":""", output, StringComparison.Ordinal);
        Assert.Empty(error);
        Assert.Equal(["-T", "-o", "StrictHostKeyChecking=yes", "--", "owner@host", "portunus-agent", "status", RunningAgent.Instance], Assert.Single(_agent.SshCalls()));
    }

    [Fact]
    public async Task PRT_PSH_004_AC2_StatusOfAReadyAgentExitsZeroWithItsAnswer()
    {
        await _agent.StartAsync();
        string value = Base64Url.EncodeToString(Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(12))));
        int received = await _agent.ReceiveAsync(Encoding.UTF8.GetBytes($$"""{"version":1,"kind":"push","vault":"Sample Test","replace":false,"items":[{"title":"Themis","fields":[{"label":"connection","value":"{{value}}"}]}]}"""));

        (int exit, string output, string error) = await StatusAsync();

        Assert.Equal(0, received);
        Assert.Equal(0, exit);
        Assert.StartsWith("""{"version":1,"state":"ready",""", output, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"push\"", output, StringComparison.Ordinal);
        Assert.Empty(error);
    }

    [Fact]
    public async Task PRT_PSH_004_AC2_StatusOfAnAgentItCannotReachExitsOne()
    {
        (int exit, string output, string _) = await StatusAsync();

        Assert.Equal(1, exit);
        Assert.Empty(output);
    }

    public async ValueTask DisposeAsync()
    {
        await _agent.DisposeAsync();
    }

    private async Task<(int Exit, string Output, string Error)> StatusAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await ToolCommand.MainAsync(["status", "--to", "owner@host", "--instance", RunningAgent.Instance], _agent.Environment, output, error);
        return (exit, output.ToString().Trim(), error.ToString());
    }
}