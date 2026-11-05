namespace Portunus.Core;

/// <summary>
/// An agent's answer to a push (<c>07</c> section 6, <c>02</c> PRT-PSH-003).
/// </summary>
internal abstract record PushAnswer
{
    private PushAnswer()
    {
    }

    /// <summary>The agent took the push's set of <paramref name="Items"/> items.</summary>
    /// <param name="Items">The set's number of items.</param>
    internal sealed record Accepted(int Items) : PushAnswer;

    /// <summary>The agent refused the push.</summary>
    /// <param name="Failure">The refusal.</param>
    internal sealed record Refused(Failure Failure) : PushAnswer;
}