using System;

namespace Portunus.Core;

/// <summary>
/// An agent's state as it answers a status request (<c>01</c> PRT-AGT-005): the state and the
/// instant it began; the reason while waiting; and while ready, the set's source, the instant
/// it was loaded and its number of items. It names no title, field or value.
/// </summary>
public sealed record AgentStatus
{
    /// <summary>The agent's state.</summary>
    public required AgentState State { get; init; }

    /// <summary>Why the agent holds no set, while <see cref="AgentState.Waiting"/>; none while ready.</summary>
    public WaitReason? Reason { get; init; }

    /// <summary>The instant the state began.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>Where the set came from, while <see cref="AgentState.Ready"/>; none while waiting.</summary>
    public SetSource? Source { get; init; }

    /// <summary>The instant the set was loaded, while <see cref="AgentState.Ready"/>; none while waiting.</summary>
    public DateTimeOffset? LoadedAt { get; init; }

    /// <summary>The set's number of items, while <see cref="AgentState.Ready"/>; 0 while waiting.</summary>
    public required int Items { get; init; }
}