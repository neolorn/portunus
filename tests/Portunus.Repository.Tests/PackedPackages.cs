using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>
/// The packages of <c>src/</c>, every project there but the agent's, which is published and not
/// packed, packed once from the working tree into a folder under <c>tmp/</c>, every build output
/// of the pack written there too.
/// </summary>
public sealed class PackedPackages : IAsyncLifetime
{
    private readonly ScratchFolder _folder = new();

    internal IReadOnlyList<PackageManifest> Manifests { get; private set; } = [];

    /// <summary>The project files of the packages under <c>src/</c>.</summary>
    internal static IReadOnlyList<string> Projects =>
        [.. ProjectFile.SourceProjects(RepositoryRoot.Path).Where(project => Packages.Packed.Contains(Path.GetFileNameWithoutExtension(project), StringComparer.Ordinal))];

    public async ValueTask InitializeAsync()
    {
        string solution = Path.Combine(_folder.Path, "Packages.sln");
        string packages = Path.Combine(_folder.Path, "packages");
        await RunAsync("new", "sln", "--name", "Packages", "--format", "sln", "--output", _folder.Path);
        foreach (string project in Projects)
        {
            await RunAsync("sln", solution, "add", project);
        }

        await RunAsync("pack", solution, "--configuration", "Release", "--artifacts-path", Path.Combine(_folder.Path, "artifacts"), "--output", packages, "-p:RestoreLockedMode=true", "-nodeReuse:false");
        Manifests = [.. Directory.EnumerateFiles(packages, "*.nupkg").Order(StringComparer.Ordinal).Select(PackageManifest.Read)];
    }

    public ValueTask DisposeAsync()
    {
        _folder.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(params string[] arguments)
    {
        ProcessResult result = await Processes.DotnetAsync(_folder.Path, TestContext.Current.CancellationToken, arguments);
        Assert.True(result.ExitCode == 0, result.Output);
    }
}