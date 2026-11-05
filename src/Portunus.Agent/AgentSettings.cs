using System;
using System.Collections.Generic;
using System.IO;

using Portunus.Core;

namespace Portunus.Agent;

/// <summary>An agent's configuration once every rule has passed (<c>01</c> PRT-AGT-002, PRT-AGT-007).</summary>
/// <param name="Instance">The instance's name.</param>
/// <param name="Options">The bound configuration.</param>
/// <param name="Declarations">The declarations, by process, in ordinal order of their names.</param>
internal sealed record AgentSettings(string Instance, AgentOptions Options, IReadOnlyList<Declaration> Declarations)
{
    /// <summary>The instance's directory, under <c>Portunus:Agent:Root</c> (<c>01</c> PRT-AGT-006).</summary>
    internal string InstanceDirectory => Path.Combine(Options.Root, Instance);

    /// <summary>The push socket's directory (<c>07</c> section 7).</summary>
    internal string PushDirectory => Path.Combine(InstanceDirectory, AgentNames.PushDirectory);

    /// <summary>The directory <c>op</c> is given as its configuration and its home: under the instance's directory, named so no process's directory can share it (<c>00</c> section 1.5).</summary>
    internal string OpDirectory => Path.Combine(InstanceDirectory, ".op");
}

/// <summary>One process's declaration (<c>01</c> PRT-AGT-007): its name, its user and group, and the addresses it reads.</summary>
/// <param name="Process">The process's name.</param>
/// <param name="Uid">Its user identifier.</param>
/// <param name="Gid">Its group identifier.</param>
/// <param name="Secrets">The addresses it reads, compared without regard to case.</param>
internal sealed record Declaration(string Process, uint Uid, uint Gid, IReadOnlySet<SecretAddress> Secrets)
{
    /// <summary>The process's directory under <paramref name="instanceDirectory"/>.</summary>
    internal string Directory(string instanceDirectory)
    {
        ArgumentNullException.ThrowIfNull(instanceDirectory);
        return Path.Combine(instanceDirectory, Process);
    }
}

/// <summary>The fixed names of <c>07</c> section 7 the agent uses.</summary>
internal static class AgentNames
{
    /// <summary>A socket's file.</summary>
    internal const string Socket = "agent.sock";

    /// <summary>The push directory.</summary>
    internal const string PushDirectory = "push";

    /// <summary>The pinned <c>op</c>'s version.</summary>
    internal const string OpVersion = "2.41.1";
}