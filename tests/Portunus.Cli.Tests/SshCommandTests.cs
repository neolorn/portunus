using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>
/// The tool cancelled while the agent's command it runs waits
/// (<c>02</c> PRT-PSH-002): the fake <c>ssh</c> runs a <c>portunus-agent</c> of the test's own,
/// a script that keeps its process identifier and waits.
/// </summary>
[Trait("Category", "linux")]
[SupportedOSPlatform("linux")]
public sealed class SshCommandTests : IDisposable
{
    private const UnixFileMode Executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private readonly string _folder = Path.Combine(Fakes.Repository, "tmp", "cli", Guid.CreateVersion7().ToString("N"));

    public SshCommandTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task MainAsync_CancelledWhileSshRuns_KillsSshAndTheCommandItStarted()
    {
        string scenario = Path.Combine(_folder, "ssh.json");
        string pid = Path.Combine(_folder, "pid");
        await File.WriteAllTextAsync(scenario, """{"recorded":false,"commands":[]}""", TestContext.Current.CancellationToken);
        string agent = Path.Combine(_folder, "portunus-agent");
        await File.WriteAllTextAsync(agent, $"#!/bin/sh\necho $$ > '{pid}'\nexec sleep 120\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(agent, Executable);
        using CancellationTokenSource cancelled = new();

        Task<int> running = ToolCommand.MainAsync(["status", "--to", "owner@host", "--instance", "sample-test"], Fakes.Environment(scenario, _folder), TextWriter.Null, TextWriter.Null, cancelled.Token);
        Stopwatch waited = Stopwatch.StartNew();
        while (!File.Exists(pid) || (await File.ReadAllTextAsync(pid, TestContext.Current.CancellationToken)).Trim().Length == 0)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The agent's command did not start within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        int command = int.Parse((await File.ReadAllTextAsync(pid, TestContext.Current.CancellationToken)).Trim(), CultureInfo.InvariantCulture);
        await cancelled.CancelAsync();

        Assert.Equal(1, await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.False(IsRunning(command), "The command ssh started still runs.");
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using Process process = Process.GetProcessById(id);
            return !process.HasExited && File.Exists($"/proc/{id}/status") && !File.ReadAllText($"/proc/{id}/status").Contains("State:\tZ", StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}