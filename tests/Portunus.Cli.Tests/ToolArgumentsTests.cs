using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>The tool's options, each checked against its rule before anything runs (<c>02</c> PRT-PSH-002; <c>07</c> section 3).</summary>
[Trait("Category", "unit")]
public sealed class ToolArgumentsTests
{
    /// <summary>An environment with no <c>PATH</c>, so a command that ran anything would answer otherwise.</summary>
    private static readonly ToolEnvironment Nothing = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("push --to host --instance sample-test", "--vault")]
    [InlineData("push --vault Sample --to host --instance sample-test", "--vault")]
    [InlineData("push --vault Sample-Test --to host --instance sample-test", "--vault")]
    [InlineData("push --vault Sample Test --instance sample-test", "--to")]
    [InlineData("push --vault Sample Test --to -oProxyCommand=x --instance sample-test", "--to")]
    [InlineData("push --vault Sample Test --to a@b@host --instance sample-test", "--to")]
    [InlineData("push --vault Sample Test --to host;reboot --instance sample-test", "--to")]
    [InlineData("push --vault Sample Test --to host", "--instance")]
    [InlineData("push --vault Sample Test --to host --instance Sample", "--instance")]
    [InlineData("push --vault Sample Test --to host --instance sample-test;reboot", "--instance")]
    [InlineData("push --vault Sample Test --to host --instance sample-test --account -x", "--account")]
    [InlineData("push --vault Sample Test --to host --instance sample-test --account", "--account")]
    [InlineData("push --vault Sample Test --to host --to other --instance sample-test", "--to")]
    [InlineData("push --vault Sample Test --to host --instance sample-test --replace --replace", "--replace")]
    [InlineData("check", "--vault")]
    [InlineData("status --to host", "--instance")]
    public async Task MainAsync_OptionMissingOrOutsideItsRule_IsRefusedNamingItBeforeAnythingRuns(string command, string member)
    {
        ArgumentNullException.ThrowIfNull(command);
        (int exit, string output, string error) = await RunAsync(Words(command));

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Equal($$$"""{"code":"secrets.cli.inputinvalid","details":{"member":"{{{member}}}"}}""", error.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("pull --vault Sample Test")]
    [InlineData("check --vault Sample Test --to host")]
    [InlineData("status --to host --instance sample-test extra")]
    public async Task MainAsync_CommandOrWordOutsideTheTable_IsAFault(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        (int exit, string _, string error) = await RunAsync(Words(command));

        Assert.Equal(1, exit);
        Assert.Equal("""{"code":"secrets.fault","details":{}}""", error.Trim());
    }

    [Fact]
    public async Task MainAsync_StatusWithNoSshOnThePath_IsRefusedWithSshMissing()
    {
        (int exit, string output, string error) = await RunAsync(["status", "--to", "host", "--instance", "sample-test"]);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Equal("""{"code":"secrets.cli.sshmissing","details":{}}""", error.Trim());
    }

    [Fact]
    public async Task MainAsync_PushWithNoOpOnThePath_IsRefusedWithOpVersionFoundEmpty()
    {
        (int exit, string output, string error) = await RunAsync(["push", "--vault", "Sample Test", "--to", "host", "--instance", "sample-test"]);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.Equal("""{"code":"secrets.cli.opversion","details":{"found":""}}""", error.Trim());
    }

    [Theory]
    [InlineData("host")]
    [InlineData("owner@host.example")]
    [InlineData("a_b-c.d")]
    public void IsDestination_WithinItsRule_IsTrue(string destination)
    {
        Assert.True(ToolArguments.IsDestination(destination));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-host")]
    [InlineData("a@b@c")]
    [InlineData("host name")]
    [InlineData("hóst")]
    public void IsDestination_OutsideItsRule_IsFalse(string destination)
    {
        Assert.False(ToolArguments.IsDestination(destination));
    }

    [Fact]
    public void IsDestination_OverTwoHundredFiftyFiveCharacters_IsFalse()
    {
        Assert.True(ToolArguments.IsDestination(new string('a', 255)));
        Assert.False(ToolArguments.IsDestination(new string('a', 256)));
    }

    [Theory]
    [InlineData("2.41.1", true)]
    [InlineData("2.41.2", true)]
    [InlineData("2.50.0", true)]
    [InlineData("2.41.0", false)]
    [InlineData("2.40.9", false)]
    [InlineData("3.0.0", false)]
    [InlineData("1.99.99", false)]
    [InlineData("2.41", false)]
    [InlineData("2.41.1-beta.01", false)]
    [InlineData("", false)]
    public void IsSupported_Version_IsThePinnedOrALaterOfItsMajor(string found, bool supported)
    {
        Assert.Equal(supported, VaultReader.IsSupported(found));
    }

    [Fact]
    public void ForOp_Environment_LeavesOutTheTokenConnectAndSessionVariables()
    {
        ToolEnvironment environment = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/owner",
            ["OP_SERVICE_ACCOUNT_TOKEN"] = "x",
            ["OP_CONNECT_HOST"] = "x",
            ["OP_CONNECT_TOKEN"] = "x",
            ["OP_SESSION_owner"] = "x",
            ["OP_ACCOUNT"] = "owner",
        });

        Assert.Equal(["HOME", "OP_ACCOUNT", "PATH"], [.. environment.ForOp.Select(variable => variable.Key).Order(StringComparer.Ordinal)]);
    }

    /// <summary>Splits <paramref name="command"/> into words, a vault title of two words kept as one.</summary>
    private static string[] Words(string command)
    {
        return command.Replace("Sample Test", "Sample\u0001Test", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Replace('\u0001', ' '))
            .ToArray();
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(string[] arguments)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await ToolCommand.MainAsync(arguments, Nothing, output, error, TestContext.Current.CancellationToken);
        return (exit, output.ToString(), error.ToString());
    }
}