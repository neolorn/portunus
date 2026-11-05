using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Portunus.Core;

/// <summary>
/// An agent's answer to a read or a status request (<c>07</c> section 6).
/// </summary>
internal abstract record WireAnswer
{
    private WireAnswer()
    {
    }

    /// <summary>The agent holds no set, for <paramref name="Reason"/>, since <paramref name="Since"/>.</summary>
    /// <param name="Reason">Why the agent holds no set.</param>
    /// <param name="Since">The instant the state began.</param>
    internal sealed record Waiting(WaitReason Reason, DateTimeOffset Since) : WireAnswer;

    /// <summary>The status of a ready agent.</summary>
    /// <param name="Since">The instant the state began.</param>
    /// <param name="Source">Where the set came from.</param>
    /// <param name="LoadedAt">The instant the set was loaded.</param>
    /// <param name="Items">The set's number of items.</param>
    internal sealed record Ready(DateTimeOffset Since, SetSource Source, DateTimeOffset LoadedAt, int Items) : WireAnswer;

    /// <summary>
    /// A secret: its current version and every live version's value, in the pinned arrays of
    /// <paramref name="Values"/>, which the answer's reader zeroes once used.
    /// </summary>
    /// <param name="Current">The number of the current version.</param>
    /// <param name="Versions">Every live version's value by its number.</param>
    /// <param name="Values">The owner of the arrays the values are in.</param>
    internal sealed record Value(int Current, IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions, PinnedValues Values) : WireAnswer
    {
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Current = ").Append(Current).Append(", Versions = [");
            builder.AppendJoin(", ", Versions.Keys.Order());
            builder.Append(']');
            return true;
        }
    }

    /// <summary>A refusal, answered in <paramref name="State"/>.</summary>
    /// <param name="State">The agent's state as it refused.</param>
    /// <param name="Failure">The refusal.</param>
    internal sealed record Refused(AgentState State, Failure Failure) : WireAnswer;
}