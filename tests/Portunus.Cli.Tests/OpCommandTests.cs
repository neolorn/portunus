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
        using CancellationTokenSource cancelled = new();

        Task<int> running = ToolCommand.MainAsync(["check", "--vault", "Sample Test"], Fakes.Environment(scenario), TextWriter.Null, TextWriter.Null, cancelled.Token);
        await Fakes.WaitForCallAsync(scenario);
        int op = Assert.Single(Recording.Calls(scenario)).ProcessId;
        await cancelled.CancelAsync();

        Assert.Equal(1, await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(await EndsAsync(op), "The op the tool started still runs.");
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// Whether the process <paramref name="id"/> ends within 10 seconds: a kill takes effect a
    /// moment after it is sent, and the fake not killed waits 120.
    /// </summary>
    private static async Task<bool> EndsAsync(int id)
    {
        try
        {
            using Process process = Process.GetProcessById(id);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            return true;
        }
        catch (ArgumentException)
        {
            // No process holds the identifier.
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}