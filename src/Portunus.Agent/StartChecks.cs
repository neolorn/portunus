using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;

using Portunus.Core;

namespace Portunus.Agent;

/// <summary>
/// The checks of the agent's start that read the system (<c>01</c> PRT-AGT-001,
/// <c>05</c> PRT-OPS-001): an active swap area, and an agent's file or a directory above it
/// that anyone but root can write.
/// </summary>
internal static class StartChecks
{
    private const uint Root = 0;

    private const UnixFileMode WritableByOthers = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    /// <summary>
    /// Answers <c>secrets.agent.swapactive</c> where the swaps file <paramref name="swaps"/>
    /// lists an area below its heading line, a compressed memory device included.
    /// </summary>
    internal static Failure? Swap(string swaps)
    {
        string[] lines = File.ReadAllLines(swaps);
        return lines.Skip(1).Any(line => line.Trim().Length > 0) ? new Failure { Code = Codes.AgentSwapActive } : null;
    }

    /// <summary>
    /// Answers <c>secrets.agent.configurationunsafe</c> where <paramref name="file"/> is not a
    /// regular file owned by root that neither its group nor others can write (<c>details.key</c>
    /// <c>file</c>), or where a directory above it is not owned by root or can be written by its
    /// group or by others (<c>details.key</c> the directory).
    /// </summary>
    internal static Failure? Configuration(string file)
    {
        FileOwnership? owned = Native.Ownership(file);
        if (owned is null || owned.Kind != FileKind.File || !IsRoots(owned))
        {
            return Unsafe("file");
        }

        for (DirectoryInfo? directory = new FileInfo(file).Directory; directory is not null; directory = directory.Parent)
        {
            FileOwnership? above = Native.Ownership(directory.FullName);
            if (above is null || above.Kind != FileKind.Directory || !IsRoots(above))
            {
                return Unsafe(directory.FullName);
            }
        }

        return null;
    }

    private static bool IsRoots(FileOwnership owned)
    {
        return owned.Owner == Root && (owned.Mode & WritableByOthers) == 0;
    }

    private static Failure Unsafe(string key)
    {
        return new Failure { Code = Codes.AgentConfigurationUnsafe, Details = new[] { ("key", key) }.ToFrozenDictionary(detail => detail.Item1, detail => detail.Item2, StringComparer.Ordinal) };
    }
}