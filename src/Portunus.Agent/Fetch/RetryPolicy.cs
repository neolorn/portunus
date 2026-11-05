using System;
using System.Diagnostics.CodeAnalysis;

namespace Portunus.Agent.Fetch;

/// <summary>
/// The pauses between failed fetches (<c>01</c> PRT-AGT-004): the first is
/// <c>Portunus:Agent:Retry:First</c>, each later one twice the one before, at most
/// <c>Portunus:Agent:Retry:Max</c>, and a rate limit makes the next one <c>Max</c> at once;
/// each is moved by a random amount of up to a fifth of itself either way, and never below
/// <c>First</c>.
/// </summary>
internal sealed class RetryPolicy(TimeSpan first, TimeSpan max, Random random)
{
    private const double Spread = 0.2;

    private TimeSpan? _pause;

    /// <summary>The pause after a failure of <paramref name="failure"/>'s class.</summary>
    [SuppressMessage("Security", "CA5394", Justification = "The pauses' spread keeps agents from asking together; it guards nothing (06 PRT-ENG-004).")]
    internal TimeSpan Next(FailureClass failure)
    {
        TimeSpan pause = failure == FailureClass.RateLimited ? max
            : _pause is TimeSpan before ? (before * 2 < max ? before * 2 : max)
            : first;
        _pause = pause;
        TimeSpan moved = pause * (1 + (((random.NextDouble() * 2) - 1) * Spread));
        return moved < first ? first : moved;
    }
}