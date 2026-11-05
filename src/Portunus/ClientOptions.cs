using System;
using System.ComponentModel.DataAnnotations;

namespace Portunus;

/// <summary>
/// The client's configuration, bound from <c>Portunus:Client</c> once each raw value has
/// passed its rule (<c>05</c> PRT-OPS-001), each member defaulting as <c>07</c> section 4 gives.
/// </summary>
internal sealed class ClientOptions
{
    /// <summary>Where the client reads; <c>agent</c> by default.</summary>
    public ClientSource Source { get; set; } = ClientSource.Agent;

    /// <summary>The agent's socket.</summary>
    [Required]
    public string Socket { get; set; } = "/run/portunus/agent.sock";

    /// <summary>The development file; none by default, and required where the source is <c>file</c>.</summary>
    public string? File { get; set; }

    /// <summary>The bound of one exchange with the agent, 1 second to 1 minute.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a read waits for the agent, 0 to 1 hour.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The pause between two asks while a read waits, 1 second to 1 minute.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}