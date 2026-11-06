using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>
/// An agent of the <c>push</c> mode run through <c>tests/Portunus.Agent.Harness</c> as root,
/// its files under a folder of root's (P-027), and the tool's environment that reaches it: the
/// fakes' <c>ssh</c>, which runs the command it is given on this machine, the fakes' <c>op</c>,
/// answering as a recorded scenario where the test names one, and a <c>portunus-agent</c> on
/// the <c>PATH</c> that runs the harness with the test's paths (<c>06</c> PRT-ENG-008). The
/// process <c>web</c> reads every secret of the sample vault "Sample Test", through the
/// harness's client as its user (P-030). Everything is removed when the test ends.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class RunningAgent : IAsyncDisposable
{
    internal const string Instance = "sample-test";

    private const UnixFileMode Open = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode Readable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private Process? _agent;

    private RunningAgent(string folder, string? scenario)
    {
        Folder = folder;
        foreach (string directory in new[] { Folder, Configurations, Root, Commands, FakesFolder })
        {
            Directory.CreateDirectory(directory, Open);
        }

        File.WriteAllText(Swaps, "Filename\t\t\t\tType\t\tSize\t\tUsed\t\tPriority\n");
        string root = JsonSerializer.Serialize(Root, RunningAgentJson.Default.String);
        File.WriteAllText(Path.Combine(Configurations, Instance + ".json"), $$$$$$"""{"Portunus":{"Agent":{"Vault":"Sample Test","PushGroup":4244,"Root":{{{{{{root}}}}}},"Source":"push","Processes":{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection","Janus/keys","Hermes/token","Ceres/password"]}}}}}""");
        File.SetUnixFileMode(Path.Combine(Configurations, Instance + ".json"), Readable);
        string script = Path.Combine(Commands, "portunus-agent");
        File.WriteAllText(script, $"#!/bin/sh\nexec dotnet '{Path.Combine(HarnessFolder, "Portunus.Agent.Harness.dll")}' '{Configurations}' '{Swaps}' \"$@\"\n");
        File.SetUnixFileMode(script, Open);
        if (scenario is null)
        {
            Scenario = Path.Combine(FakesFolder, "ssh.json");
            File.WriteAllText(Scenario, """{"recorded":false,"commands":[]}""");
        }
        else
        {
            Scenario = Recording.Copy(scenario, FakesFolder);
        }
    }

    /// <summary>The repository's root.</summary>
    internal static string Repository { get; } = RepositoryRoot();

    /// <summary>The folder the harness is built in.</summary>
    internal static string HarnessFolder { get; } = Path.Combine(Repository, "tests", "Portunus.Agent.Harness", "bin", "Release", "net10.0");

    /// <summary>The folder the fakes are built in, holding <c>ssh</c>.</summary>
    internal static string Fakes { get; } = Path.Combine(Repository, "tests", "Portunus.Fakes", "bin", "Release", "net10.0");

    internal string Folder { get; }

    /// <summary>The fakes' scenario, beside which they keep their calls.</summary>
    internal string Scenario { get; }

    /// <summary>The folder of the fakes' scenario and its files, which hold the recorded values where the scenario is recorded.</summary>
    internal string FakesFolder => Path.Combine(Folder, "fakes");

    /// <summary>The tool's environment: the fakes and the agent's command on the <c>PATH</c>, the fakes' scenario, and the SDK's own variables.</summary>
    internal ToolEnvironment Environment
    {
        get
        {
            Dictionary<string, string> variables = new(StringComparer.Ordinal)
            {
                ["PATH"] = string.Join(Path.PathSeparator, Fakes, Commands, System.Environment.GetEnvironmentVariable("PATH")),
                ["PORTUNUS_FAKES_SCENARIO"] = Scenario,
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

    /// <summary>A new folder of root's under <c>/run</c>, the agent not yet started, the fakes answering as the recorded <paramref name="scenario"/> where one is named.</summary>
    internal static RunningAgent Prepared(string? scenario = null)
    {
        Directory.CreateDirectory("/run/portunus-tests", Open);
        return new RunningAgent(Path.Combine("/run", "portunus-tests", Guid.CreateVersion7().ToString("N")[^12..]), scenario);
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

    /// <summary>The arguments of each call the fake <c>ssh</c> took, whose first is its <c>-T</c>.</summary>
    internal IReadOnlyList<string[]> SshCalls()
    {
        return [.. Recording.Calls(Scenario).Select(call => call.Arguments).Where(arguments => arguments is ["-T", ..])];
    }

    /// <summary>
    /// Reads <paramref name="address"/> on the socket of <c>web</c> through the harness's
    /// client run under <c>setpriv</c> as its user and group (P-030), and answers the answer's
    /// text. The harness and the request are copied under the test's folder, which that user can
    /// read, since the repository may not be.
    /// </summary>
    internal async Task<string> ReadAsync(string address)
    {
        string harness = Path.Combine(Folder, "harness");
        if (!Directory.Exists(harness))
        {
            foreach (string file in Directory.EnumerateFiles(HarnessFolder, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(harness, Path.GetRelativePath(HarnessFolder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        string request = Path.Combine(Folder, "request-" + Guid.CreateVersion7().ToString("N"));
        await File.WriteAllTextAsync(request, $$"""{"version":1,"kind":"read","address":"{{address}}"}""", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(request, Readable);
        ProcessStartInfo start = new("setpriv") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "--reuid=4243", "--regid=4242", "--clear-groups", "dotnet", Path.Combine(harness, "Portunus.Agent.Harness.dll"), "client", Path.Combine(Root, Instance, "web", "agent.sock"), request })
        {
            start.ArgumentList.Add(argument);
        }

        using Process client = Process.Start(start)!;
        Task<string> output = client.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = client.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await client.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(client.ExitCode == 0, await error);
        return await output;
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