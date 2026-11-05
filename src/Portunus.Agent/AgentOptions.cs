using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

namespace Portunus.Agent;

/// <summary>
/// The agent's configuration, bound from <c>Portunus:Agent</c> once each raw value has passed
/// its rule (<c>05</c> PRT-OPS-001), each member defaulting as <c>07</c> section 4 gives.
/// </summary>
internal sealed class AgentOptions
{
    /// <summary>The vault's title.</summary>
    [Required]
    public string Vault { get; set; } = string.Empty;

    /// <summary>Where the agent takes its set: <c>vault</c> by default.</summary>
    public AgentSource Source { get; set; } = AgentSource.Vault;

    /// <summary>The directory the instances' directories are in.</summary>
    [Required]
    public string Root { get; set; } = "/run/portunus";

    /// <summary>The token's file; by default the file <c>token</c> of the directory <c>CREDENTIALS_DIRECTORY</c> names.</summary>
    public string? TokenFile { get; set; }

    /// <summary>The pinned <c>op</c>.</summary>
    [Required]
    public string OpPath { get; set; } = "/usr/local/bin/op";

    /// <summary>The bound of one <c>op</c> command, 5 seconds to 5 minutes.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
    public TimeSpan OpTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>The pauses between fetches.</summary>
    [ValidateObjectMembers]
    public RetryOptions Retry { get; set; } = new();

    /// <summary>The push socket's group.</summary>
    [Range(1u, 4294967294u)]
    public uint PushGroup { get; set; }

    /// <summary>The declarations, by process.</summary>
    public Dictionary<string, DeclarationOptions> Processes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>The pauses between fetches (<c>01</c> PRT-AGT-004).</summary>
internal sealed class RetryOptions
{
    /// <summary>The first pause, 5 seconds to 10 minutes.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan First { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The longest pause, from <see cref="First"/> to 1 day.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "1.00:00:00")]
    public TimeSpan Max { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>One process's declaration (<c>01</c> PRT-AGT-007).</summary>
internal sealed class DeclarationOptions
{
    /// <summary>The process's user identifier.</summary>
    [Range(1u, 4294967294u)]
    public uint Uid { get; set; }

    /// <summary>The process's group identifier.</summary>
    [Range(1u, 4294967294u)]
    public uint Gid { get; set; }

    /// <summary>The addresses the process reads.</summary>
    [Required]
    public List<string> Secrets { get; set; } = [];
}

/// <summary>Where the agent takes its set: the closed value <c>Portunus:Agent:Source</c> of <c>07</c> section 2.</summary>
internal enum AgentSource
{
    /// <summary>From the vault, by a fetch (<c>01</c> PRT-AGT-003); spelled <c>vault</c>.</summary>
    Vault,

    /// <summary>From a push alone (<c>02</c> PRT-PSH-005); spelled <c>push</c>.</summary>
    Push,
}