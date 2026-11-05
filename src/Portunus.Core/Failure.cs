using System.Collections.Frozen;
using System.Collections.Generic;

namespace Portunus.Core;

/// <summary>
/// A refusal an operation answers: a code of the library's reference and its details, such as
/// <c>reason</c> for <c>secrets.agent.waittimeout</c> (<c>06</c> PRT-ENG-003). Its details
/// name no value and no token.
/// </summary>
public sealed record Failure
{
    /// <summary>The code, such as <c>secrets.secret.denied</c>.</summary>
    public required string Code { get; init; }

    /// <summary>The code's details, by name; none where the code has none.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } = FrozenDictionary<string, string>.Empty;
}