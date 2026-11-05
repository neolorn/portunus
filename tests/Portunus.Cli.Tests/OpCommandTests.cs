using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>The tool cancelled while the fake <c>op</c> waits (<c>02</c> PRT-PSH-002).</summary>
[Trait("Category", "integration")]
public sealed class OpCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(Fakes.Repository, "tmp", "cli", Guid.CreateVersion7().ToString("N"));

    public OpCommandTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task MainAsync_CancelledWhileOpRuns_KillsOp()
    {
        string scenario = Path.Combine(_folder, "scenario.json");
        await File.WriteAllTextAsync(scenario, """{"recorded":false,"commands":[{"arguments":["--version"],"output":null,"error":null,"exitCode":0,"pauseMilliseconds":120000}]}""", TestContext.Current.CancellationToken);
        DateTime started = DateTime.Now.AddSeconds(-1);
        using CancellationTokenSource cancelled = new();

        Task<int> running = ToolCommand.MainAsync(["check", "--vault", "Sample Test"], Fakes.Environment(scenario), TextWriter.Null, TextWriter.Null, cancelled.Token);
        await Fakes.WaitForCallAsync(scenario);
        await cancelled.CancelAsync();

        Assert.Equal(1, await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(Process.GetProcessesByName("op"), process => Started(process) >= started);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static DateTime Started(Process process)
    {
        try
        {
            return process.HasExited ? DateTime.MinValue : process.StartTime;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process ended while it was read.
            return DateTime.MinValue;
        }
    }
}