using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Portunus.Core;

/// <summary>
/// What a process reads at one address (<c>03</c> PRT-VLT-004): every live version of the
/// secret by its number, and the current one; a single secret is version 1, current 1. The
/// arrays behind the values are the reader's own, and zeroing them once used is the reader's
/// (<c>06</c> PRT-ENG-005). It prints its version numbers and no value.
/// </summary>
public sealed record Secret
{
    /// <summary>The number of the current version, one of <see cref="Versions"/>.</summary>
    public required int Current { get; init; }

    /// <summary>Every live version's value by its number, <see cref="Current"/> among them.</summary>
    public required IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions { get; init; }

    /// <summary>The current version's value.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Versions"/> holds no version <see cref="Current"/>.</exception>
    public ReadOnlyMemory<byte> CurrentValue =>
        Versions.TryGetValue(Current, out ReadOnlyMemory<byte> value) ? value : throw new InvalidOperationException("A secret's versions hold its current version.");

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Current = ").Append(Current).Append(", Versions = [");
        builder.AppendJoin(", ", Versions.Keys.Order());
        builder.Append(']');
        return true;
    }
}