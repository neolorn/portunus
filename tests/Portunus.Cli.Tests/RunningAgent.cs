using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>
/// An agent of the <c>push</c> mode run through <c>tests/Portunus.Agent.Harness</c> as root,
/// its files under a folder of root's (P-027), and the tool's environment that reaches it: the
/// fakes' <c>ssh</c>, which runs the command it is given on this machine, and a
/// <c>portunus-agent</c> on the <c>PATH</c> that runs the harness with the test's paths
/// (<c>06</c> PRT-ENG-008). Everything is removed when the test ends.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class RunningAgent : IAsyncDisposable
{
    internal const string Instance = "sample-test";

    private const UnixFileMode Open = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode Readable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private Process? _agent;

    private RunningAgent(string folder)
    {
        Folder = folder;
        foreach (string directory in new[] { Folder, Configurations, Root, Commands })
        {
            Directory.CreateDirectory(directory, Open);
        }

        File.WriteAllText(Swaps, "Filename\t\t\t\tType\t\tSize\t\tUsed\t\tPriority\n");
        string root = JsonSerializer.Serialize(Root, RunningAgentJson.Default.String);
        File.WriteAllText(Path.Combine(Configurations, Instance + ".json"), $$$$$$"""{"Portunus":{"Agent":{"Vault":"Sample Test","PushGroup":4244,"Root":{{{{{{root}}}}}},"Source":"push","Processes":{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection"]}}}}}""");
        File.SetUnixFileMode(Path.Combine(Configurations, Instance + ".json"), Readable);
        string script = Path.Combine(Commands, "portunus-agent");
        File.WriteAllText(script, $"#!/bin/sh\nexec dotnet '{Path.Combine(HarnessFolder, "Portunus.Agent.Harness.dll")}' '{Configurations}' '{Swaps}' \"$@\"\n");
        File.SetUnixFileMode(script, Open);
        File.WriteAllText(SshScenario, """{"recorded":false,"commands":[]}""");
    }

    /// <summary>The repository's root.</summary>
    internal static string Repository { get; } = RepositoryRoot();

    /// <summary>The folder the harness is built in.</summary>
    internal static string HarnessFolder { get; } = Path.Combine(Repository, "tests", "Portunus.Agent.Harness", "bin", "Release", "net10.0");

    /// <summary>The folder the fakes are built in, holding <c>ssh</c>.</summary>
    internal static string Fakes { get; } = Path.Combine(Repository, "tests", "Portunus.Fakes", "bin", "Release", "net10.0");

    internal string Folder { get; }

    /// <summary>The fake <c>ssh</c>'s scenario, beside which it keeps its calls.</summary>
    internal string SshScenario => Path.Combine(Folder, "ssh.json");

    /// <summary>The tool's environment: the fakes and the agent's command on the <c>PATH</c>, the fakes' scenario, and the SDK's own variables.</summary>
    internal ToolEnvironment Environment
    {
        get
        {
            Dictionary<string, string> variables = new(StringComparer.Ordinal)
            {
                ["PATH"] = string.Join(Path.PathSeparator, Fakes, Commands, System.Environment.GetEnvironmentVariable("PATH")),
                ["PORTUNUS_FAKES_SCENARIO"] = SshScenario,
            };
            if (System.Environment.GetEnvironmentVariable("DOTNET_ROOT") is string dotnetRoot)
            {
                variables["DOTNET_ROOT"] = dotnetRoot;
            }

            return new ToolEnvironment(variables);
        }
    }

    private string Configurations => Path.Combine(Folder, "etc");

    private string Root => Path.Combine(Folder, "run");

    private string Swaps => Path.Combine(Folder, "swaps");

    private string Commands => Path.Combine(Folder, "bin");

    private string PushSocket => Path.Combine(Root, Instance, "push", "agent.sock");

    /// <summary>A new folder of root's under <c>/run</c>, the agent not yet started.</summary>
    internal static RunningAgent Prepared()
    {
        Directory.CreateDirectory("/run/portunus-tests", Open);
        return new RunningAgent(Path.Combine("/run", "portunus-tests", Guid.CreateVersion7().ToString("N")[^12..]));
    }

    /// <summary>Starts the agent and waits for its push socket.</summary>
    internal async Task StartAsync()
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { Path.Combine(HarnessFolder, "Portunus.Agent.Harness.dll"), Configurations, Swaps, "run", Instance })
        {
            start.ArgumentList.Add(argument);
        }

        _agent = Process.Start(start)!;
        Stopwatch waited = Stopwatch.StartNew();
        while (!File.Exists(PushSocket))
        {
            Assert.False(_agent.HasExited, "The agent stopped.");
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The agent did not listen within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Runs <c>portunus-agent receive</c> with <paramref name="payload"/>, as <c>ssh</c> would on the server, and answers its exit code.</summary>
    internal async Task<int> ReceiveAsync(byte[] payload)
    {
        ProcessStartInfo start = new(Path.Combine(Commands, "portunus-agent")) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("receive");
        start.ArgumentList.Add(Instance);
        using Process receive = Process.Start(start)!;
        await receive.StandardInput.BaseStream.WriteAsync(payload, TestContext.Current.CancellationToken);
        receive.StandardInput.Close();
        await receive.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await receive.WaitForExitAsync(TestContext.Current.CancellationToken);
        return receive.ExitCode;
    }

    /// <summary>The arguments of each call the fake <c>ssh</c> took.</summary>
    internal IReadOnlyList<string[]> SshCalls()
    {
        string calls = SshScenario + ".calls";
        List<string[]> read = [];
        if (File.Exists(calls))
        {
            foreach (string line in File.ReadAllLines(calls))
            {
                List<string> arguments = [];
                foreach (JsonElement argument in JsonDocument.Parse(line).RootElement.GetProperty("arguments").EnumerateArray())
                {
                    arguments.Add(argument.GetString()!);
                }

                read.Add([.. arguments]);
            }
        }

        return read;
    }

    public async ValueTask DisposeAsync()
    {
        if (_agent is not null)
        {
            if (!_agent.HasExited)
            {
                _agent.Kill(entireProcessTree: true);
                await _agent.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            _agent.Dispose();
        }

        Directory.Delete(Folder, recursive: true);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Portunus.sln")))
        {
            root = root.Parent;
        }

        return root?.FullName ?? throw new InvalidOperationException("The tests run below the repository's root.");
    }
}

/// <summary>The test's source-generated JSON context, for a path written as a JSON string (<c>06</c> PRT-ENG-004).</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class RunningAgentJson : JsonSerializerContext;