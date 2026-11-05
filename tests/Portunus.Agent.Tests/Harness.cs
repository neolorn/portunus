using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>
/// <c>tests/Portunus.Agent.Harness</c>, built beside the tests: the agent as an executable
/// (P-027), and a process's client run under <c>setpriv</c> as the declared user (P-030).
/// </summary>
internal static class Harness
{
    /// <summary>The declared process <c>web</c>'s user and group, as <see cref="AgentFiles.WriteConfiguration"/> writes them.</summary>
    internal const uint WebUser = 4243;

    internal const uint WebGroup = 4242;

    /// <summary>The repository's root.</summary>
    internal static string Repository { get; } = RepositoryRoot();

    /// <summary>The folder the harness is built in.</summary>
    internal static string Folder { get; } = Path.Combine(Repository, "tests", "Portunus.Agent.Harness", "bin", "Release", "net10.0");

    /// <summary>Starts the harness with <paramref name="arguments"/>, its output and error redirected.</summary>
    internal static Process Start(string harness, params string[] arguments)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(harness, "Portunus.Agent.Harness.dll"));
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start)!;
    }

    /// <summary>
    /// Sends <paramref name="request"/> on <paramref name="socket"/> as user <paramref name="user"/>
    /// and group <paramref name="group"/>, its sending side shut down after <paramref name="pause"/>,
    /// and answers the answer's text, or <c>no answer</c>. The harness and the request are copied
    /// under <paramref name="files"/>, which that user can read, since the repository may not be.
    /// </summary>
    internal static async Task<string> ClientAsync(AgentFiles files, string socket, byte[] request, uint user = WebUser, uint group = WebGroup, TimeSpan pause = default)
    {
        string harness = Path.Combine(files.Folder, "harness");
        if (!Directory.Exists(harness))
        {
            Copy(Folder, harness);
        }

        string requestFile = Path.Combine(files.Folder, "request-" + Guid.CreateVersion7().ToString("N"));
        await File.WriteAllBytesAsync(requestFile, request, TestContext.Current.CancellationToken);
        File.SetUnixFileMode(requestFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        ProcessStartInfo start = new("setpriv") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[]
        {
            "--reuid=" + user.ToString(CultureInfo.InvariantCulture),
            "--regid=" + group.ToString(CultureInfo.InvariantCulture),
            "--clear-groups",
            "dotnet",
            Path.Combine(harness, "Portunus.Agent.Harness.dll"),
            "client",
            socket,
            requestFile,
            ((int)pause.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
        })
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

    /// <summary>The request's bytes of <paramref name="json"/>.</summary>
    internal static byte[] Request(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
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