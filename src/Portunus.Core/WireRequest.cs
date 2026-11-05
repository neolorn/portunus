namespace Portunus.Core;

/// <summary>
/// A request on an agent's socket (<c>07</c> section 6): a read, a status request or a push.
/// </summary>
internal abstract record WireRequest
{
    private WireRequest()
    {
    }

    /// <summary>A read of the secret at <paramref name="Address"/>.</summary>
    /// <param name="Address">The secret's address.</param>
    internal sealed record Read(SecretAddress Address) : WireRequest;

    /// <summary>A request of the agent's status.</summary>
    internal sealed record Status : WireRequest;

    /// <summary>
    /// A push of <paramref name="Set"/>, whose values are in the pinned arrays of
    /// <paramref name="Values"/>, which the request's taker zeroes once used.
    /// </summary>
    /// <param name="Set">The pushed set; its items carry no note, which a push does not send.</param>
    /// <param name="Replace">Whether the push replaces the set of a ready agent.</param>
    /// <param name="Values">The owner of the arrays the set's values are in.</param>
    internal sealed record Push(VaultSet Set, bool Replace, PinnedValues Values) : WireRequest;
}