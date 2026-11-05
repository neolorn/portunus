using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

// The agent is a Linux executable, and its tests call it as one (00 section 2.2).
[assembly: SupportedOSPlatform("linux")]

namespace Portunus.Agent.Tests;

/// <summary>
/// The files an agent's test writes: its configuration and its swaps file, under the
/// repository's <c>tmp/</c> for a test that reads them alone, or under a folder of root's
/// for a test of the start, which checks that the file and every directory above it are root's
/// (<c>05</c> PRT-OPS-001, P-027). Everything is removed when the test ends.
/// </summary>
internal sealed class AgentFiles : IDisposable
{
    internal const string Instance = "sample-test";

    private const UnixFileMode Readable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private const UnixFileMode Open = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private AgentFiles(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(Configurations);
        Directory.CreateDirectory(Root);
        System.IO.File.WriteAllText(Swaps, "Filename\t\t\t\tType\t\tSize\t\tUsed\t\tPriority\n");
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(Configurations, Open);
            System.IO.File.SetUnixFileMode(Root, Open);
        }
    }

    /// <summary>The folder every file of the test is under.</summary>
    internal string Folder { get; }

    /// <summary>The directory of the agents' files.</summary>
    internal string Configurations => Path.Combine(Folder, "etc");

    /// <summary>The directory the instances' directories are in.</summary>
    internal string Root => Path.Combine(Folder, "run");

    /// <summary>The swaps file, listing no area.</summary>
    internal string Swaps => Path.Combine(Folder, "swaps");

    /// <summary>The instance's configuration file.</summary>
    internal string File => Path.Combine(Configurations, Instance + ".json");

    /// <summary>The instance's directory.</summary>
    internal string InstanceDirectory => Path.Combine(Root, Instance);

    internal AgentPaths Paths => new(Configurations, Swaps);

    /// <summary>A folder under the repository's <c>tmp/</c>, for a test that reads a configuration alone.</summary>
    internal static AgentFiles Scratch()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "Portunus.sln")))
        {
            folder = folder.Parent;
        }

        return new AgentFiles(Path.Combine(folder?.FullName ?? throw new InvalidOperationException("The tests run below the repository's root."), "tmp", "agent", Name()));
    }

    /// <summary>A folder of root's under <c>/run</c>, mode <c>0755</c>, for a test of the start, which runs as root (P-027).</summary>
    internal static AgentFiles OfRoot()
    {
        string folder = Path.Combine("/run", "portunus-tests", Name());
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!, Open);
        Directory.CreateDirectory(folder, Open);
        return new AgentFiles(folder);
    }

    /// <summary>
    /// Writes the instance's configuration: the vault title <c>Sample Test</c>, the push group
    /// 4244, the root, the source <c>push</c> and one process <c>web</c>, user 4243 and group
    /// 4242, reading <c>Themis/connection</c>; each member of <paramref name="members"/>, a raw
    /// JSON value, replaces or adds a member of <c>Portunus:Agent</c>, and a null removes it.
    /// </summary>
    internal void WriteConfiguration(params (string Name, string? Json)[] members)
    {
        Dictionary<string, string?> agent = new(StringComparer.Ordinal)
        {
            ["Vault"] = "\"Sample Test\"",
            ["PushGroup"] = "4244",
            ["Root"] = JsonSerializer.Serialize(Root, AgentFilesJson.Default.String),
            ["Source"] = "\"push\"",
            ["Processes"] = """{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection"]}}""",
        };
        foreach ((string name, string? json) in members)
        {
            agent[name] = json;
        }

        string written = string.Join(",", agent.Where(member => member.Value is not null).Select(member => $"\"{member.Key}\":{member.Value}"));
        System.IO.File.WriteAllText(File, "{\"Portunus\":{\"Agent\":{" + written + "}}}");
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(File, Readable);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    private static string Name()
    {
        return Guid.CreateVersion7().ToString("N")[^12..];
    }
}

/// <summary>The test's source-generated JSON context, for a path written as a JSON string (<c>06</c> PRT-ENG-004).</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class AgentFilesJson : JsonSerializerContext;