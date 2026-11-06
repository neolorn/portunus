using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>
/// The tool's read of a vault through the fake <c>op</c>, answering as the owner's recordings
/// of the sample vaults do, and its refusals before <c>ssh</c> runs (<c>02</c> PRT-PSH-002,
/// PRT-PSH-004).
/// </summary>
[Trait("Category", "integration")]
public sealed class ToolVaultTests : IDisposable
{
    private readonly string _folder = Path.Combine(Fakes.Repository, "tmp", "cli", Guid.CreateVersion7().ToString("N"));

    public ToolVaultTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task PRT_PSH_004_AC1_CheckOfAVaultWithOneErrorAndOneWarningWritesBothAndExitsOne()
    {
        (int exit, string[] lines, string error) = await RunAsync("sample-broken-tool", "check", "--vault", "Sample Broken");

        Assert.Equal(1, exit);
        Assert.Equal(["""{"code":"secrets.layout.fieldname","item":"Themis","field":"bad label"}""", """{"code":"secrets.layout.nonote","item":"Hermes"}"""], lines.Order(StringComparer.Ordinal));
        Assert.Empty(error);
    }

    [Fact]
    public async Task PRT_PSH_004_AC1_CheckOfAVaultWithAWarningAloneWritesItAndExitsZero()
    {
        (int exit, string[] lines, string error) = await RunAsync("sample-test-tool", "check", "--vault", "Sample Test");

        Assert.Equal(0, exit);
        Assert.Equal(["""{"code":"secrets.layout.nonote","item":"Ceres"}"""], lines);
        Assert.Empty(error);
    }

    [Fact]
    public async Task PRT_PSH_002_AC2_PushOfAVaultBreakingTheLayoutIsRefusedNamingItemAndFieldAndSshNeverRuns()
    {
        (int exit, string[] lines, string error) = await RunAsync("sample-broken-tool", "push", "--vault", "Sample Broken", "--to", "owner@host", "--instance", "sample-test");

        Assert.Equal(1, exit);
        Assert.Empty(lines);
        Assert.Equal("""{"code":"secrets.layout.fieldname","details":{"field":"bad label","item":"Themis"}}""", error.Trim());
        Assert.Equal(4, Recording.Calls(Scenario("sample-broken-tool")).Count);
        Assert.All(Recording.Calls(Scenario("sample-broken-tool")), call => Assert.NotEqual("-T", call.Arguments[0]));
    }

    [Fact]
    public async Task PRT_PSH_002_AC4_OpBelowThePinnedVersionIsRefusedWithOpVersion()
    {
        (int exit, string[] lines, string error) = await RunAsync("wrong-version", "push", "--vault", "Sample Test", "--to", "owner@host", "--instance", "sample-test");

        Assert.Equal(1, exit);
        Assert.Empty(lines);
        Assert.Equal("""{"code":"secrets.cli.opversion","details":{"found":"2.41.0"}}""", error.Trim());
        Assert.Equal(["--version"], Assert.Single(Recording.Calls(Scenario("wrong-version"))).Arguments);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string Scenario(string scenario)
    {
        return Path.Combine(_folder, scenario + ".json");
    }

    /// <summary>Runs the tool with the fakes answering as <paramref name="scenario"/>, and answers its exit code, its lines of standard output and its standard error.</summary>
    private async Task<(int Exit, string[] Lines, string Error)> RunAsync(string scenario, params string[] arguments)
    {
        string copy = Recording.Copy(scenario, _folder);
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await ToolCommand.MainAsync(arguments, Fakes.Environment(copy), output, error, TestContext.Current.CancellationToken);
        return (exit, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), error.ToString());
    }
}