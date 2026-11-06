using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Cli.Tests;

/// <summary>The fakes as the tool's tests run them: on the <c>PATH</c> of the tool's environment, with a scenario of the test's.</summary>
internal static class Fakes
{
    /// <summary>The repository's root.</summary>
    internal static string Repository { get; } = RepositoryRoot();

    /// <summary>The folder the fakes are built in, holding <c>op</c> and <c>ssh</c>.</summary>
    internal static string Folder { get; } = Path.Combine(Repository, "tests", "Portunus.Fakes", "bin", "Release", "net10.0");

    /// <summary>The tool's environment: <paramref name="folders"/> and the fakes on the <c>PATH</c>, <paramref name="scenario"/> the fakes' scenario.</summary>
    internal static ToolEnvironment Environment(string scenario, params string[] folders)
    {
        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = string.Join(Path.PathSeparator, [.. folders, Folder, System.Environment.GetEnvironmentVariable("PATH")]),
            ["PATHEXT"] = ".EXE",
            ["PORTUNUS_FAKES_SCENARIO"] = scenario,
        };
        foreach (string name in new[] { "DOTNET_ROOT", "SystemRoot" })
        {
            if (System.Environment.GetEnvironmentVariable(name) is string value)
            {
                variables[name] = value;
            }
        }

        return new ToolEnvironment(variables);
    }

    /// <summary>Waits until a fake has kept its first call beside <paramref name="scenario"/>.</summary>
    internal static async Task WaitForCallAsync(string scenario)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (Recording.Calls(scenario).Count == 0)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The fake was not called within the test's patience.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
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