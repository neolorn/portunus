using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>The agent run as an executable through the harness, stopped by a real <c>SIGTERM</c> (<c>01</c> PRT-AGT-002, PRT-AGT-009; P-027).</summary>
[Trait("Category", "linux")]
public sealed class AgentSignalTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly AgentFiles _files = AgentFiles.OfRoot();

    [Fact]
    public async Task PRT_AGT_009_AC1_SigtermStopsTheAgentWithExitZeroWithinTenSecondsItsSocketsRemoved()
    {
        _files.WriteConfiguration();
        using Process agent = StartHarness();
        string push = Path.Combine(_files.InstanceDirectory, "push", "agent.sock");
        await WaitForAsync(() => File.Exists(push));

        Stopwatch stopping = Stopwatch.StartNew();
        using (Process kill = Process.Start("kill", ["-TERM", agent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]))
        {
            await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        await agent.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, agent.ExitCode);
        Assert.True(stopping.Elapsed < TimeSpan.FromSeconds(10));
        Assert.False(File.Exists(push));
        Assert.False(File.Exists(Path.Combine(_files.InstanceDirectory, "web", "agent.sock")));
        Assert.Contains("agent sample-test stopped", await agent.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_AGT_002_AC2_PushModeStartsWithNoTokenFileAndNoOp()
    {
        string missing = Path.Combine(_files.Folder, "missing");
        _files.WriteConfiguration(("TokenFile", $"\"{missing}\""), ("OpPath", $"\"{missing}\""));
        using Process agent = StartHarness();
        string push = Path.Combine(_files.InstanceDirectory, "push", "agent.sock");

        await WaitForAsync(() => File.Exists(push) || agent.HasExited);

        Assert.False(agent.HasExited);
        Assert.False(Directory.Exists(Path.Combine(_files.InstanceDirectory, ".op")));
        agent.Kill(entireProcessTree: true);
        await agent.WaitForExitAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private Process StartHarness()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Portunus.sln")))
        {
            root = root.Parent;
        }

        string harness = Path.Combine(root!.FullName, "tests", "Portunus.Agent.Harness", "bin", "Release", "net10.0", "Portunus.Agent.Harness.dll");
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { harness, _files.Configurations, _files.Swaps, "run", AgentFiles.Instance })
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start)!;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(waited.Elapsed < Patience, "The agent did not get there within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }
}