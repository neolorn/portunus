using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>The checks of the agent's start, each refusal one line on standard error and exit code 1 (<c>01</c> PRT-AGT-001, PRT-AGT-002; <c>05</c> PRT-OPS-001).</summary>
[Trait("Category", "linux")]
public sealed class AgentStartTests : IDisposable
{
    private readonly AgentFiles _files = AgentFiles.OfRoot();

    [Fact]
    public async Task PRT_AGT_001_AC1_ActiveSwapAreaRefusesTheStartAndReadsNothing()
    {
        await File.AppendAllTextAsync(_files.Swaps, "/dev/zram0                              partition\t4194300\t\t0\t\t100\n", TestContext.Current.CancellationToken);

        // No configuration is written: a start that read it would refuse it as unsafe instead.
        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        Assert.Equal("secrets.agent.swapactive", line.GetProperty("code").GetString());
        Assert.False(Directory.Exists(_files.InstanceDirectory));
    }

    [Fact]
    public void PRT_AGT_001_AC2_AgentMarkedNotDumpableAnswersZero()
    {
        // The start's first step, before it reads anything.
        Native.MarkNotDumpable();

        Assert.Equal(0, Native.Dumpable());
    }

    [Fact]
    public async Task PRT_OPS_001_AC2_FileWritableByItsGroupStopsTheStart()
    {
        _files.WriteConfiguration();
        File.SetUnixFileMode(_files.File, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.configurationunsafe", ("key", "file"));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_DirectoryAboveTheFileWritableByOthersStopsTheStartNamingIt()
    {
        _files.WriteConfiguration();
        File.SetUnixFileMode(_files.Configurations, (UnixFileMode)0b111_111_111);

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.configurationunsafe", ("key", _files.Configurations));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_KeyOutsideItsRuleStopsTheStartNamingIt()
    {
        _files.WriteConfiguration(("OpTimeout", "\"00:00:01\""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.configurationinvalid", ("key", "Portunus:Agent:OpTimeout"));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_DeclarationOutsideItsRuleStopsTheStartNamingProcessAndMember()
    {
        _files.WriteConfiguration(("Processes", """{"web":{"Uid":4243,"Gid":4242,"Secrets":[]}}"""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.declarationinvalid", ("member", "Secrets"), ("process", "web"));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_OpThatDoesNotRunStopsTheStartWithOpVersionFoundEmpty()
    {
        string missing = Path.Combine(_files.Folder, "missing");
        _files.WriteConfiguration(("Source", "\"vault\""), ("OpPath", $"\"{missing}\""), ("TokenFile", $"\"{missing}\""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.opversion", ("found", string.Empty));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_OpAnsweringNoVersionStopsTheStartHavingRunOnlyOpVersion()
    {
        FakeOp op = FakeOp.AnsweringNothing(_files);
        string missing = Path.Combine(_files.Folder, "missing");
        _files.WriteConfiguration(("Source", "\"vault\""), ("OpPath", $"\"{op.Launcher}\""), ("TokenFile", $"\"{missing}\""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.opversion", ("found", string.Empty));
        Assert.Equal(["--version"], Assert.Single(op.Calls()));
    }

    [Fact]
    public async Task PRT_AGT_002_AC1_OpOfAnotherVersionStopsTheStartNamingTheVersionFound()
    {
        FakeOp op = FakeOp.Answering(_files, "wrong-version");
        string missing = Path.Combine(_files.Folder, "missing");
        _files.WriteConfiguration(("Source", "\"vault\""), ("OpPath", $"\"{op.Launcher}\""), ("TokenFile", $"\"{missing}\""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.opversion", ("found", "2.41.0"));
        Assert.Equal(["--version"], Assert.Single(op.Calls()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task PRT_AGT_002_AC1_TokenFileMissingOrEmptyStopsTheStartWithTokenUnavailableHavingRunOnlyOpVersion(string? content)
    {
        FakeOp op = FakeOp.Answering(_files, "sample-test");
        string token = Path.Combine(_files.Folder, "token");
        if (content is not null)
        {
            await File.WriteAllTextAsync(token, content, TestContext.Current.CancellationToken);
        }

        _files.WriteConfiguration(("Source", "\"vault\""), ("OpPath", $"\"{op.Launcher}\""), ("TokenFile", $"\"{token}\""));

        (int exit, JsonElement line) = await RunAsync();

        Assert.Equal(1, exit);
        AssertRefusal(line, "secrets.agent.tokenunavailable");
        Assert.Equal(["--version"], Assert.Single(op.Calls()));
    }

    [Theory]
    [InlineData("go")]
    [InlineData("run")]
    [InlineData("run Sample")]
    public async Task MainAsync_CommandOutsideItsRule_IsAFault(string command)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        ArgumentNullException.ThrowIfNull(command);
        int exit = await AgentCommand.MainAsync(command.Split(' '), _files.Paths, Stream.Null, output, error);

        Assert.Equal(1, exit);
        Assert.Equal("""{"code":"secrets.fault","details":{}}""", error.ToString().Trim());
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private static void AssertRefusal(JsonElement line, string code, params (string Name, string Value)[] details)
    {
        Assert.Equal(code, line.GetProperty("code").GetString());
        JsonElement written = line.GetProperty("details");
        Assert.Equal(details.Length, written.EnumerateObject().Count());
        foreach ((string name, string value) in details)
        {
            Assert.Equal(value, written.GetProperty(name).GetString());
        }
    }

    /// <summary>Runs <c>run</c>, answering its exit code and its one line of standard error; no socket is made.</summary>
    private async Task<(int Exit, JsonElement Line)> RunAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = await AgentCommand.MainAsync(["run", AgentFiles.Instance], _files.Paths, Stream.Null, output, error);
        string[] lines = error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Empty(output.ToString());
        Assert.False(File.Exists(Path.Combine(_files.InstanceDirectory, "web", "agent.sock")));
        return (exit, JsonDocument.Parse(lines[0]).RootElement.Clone());
    }
}