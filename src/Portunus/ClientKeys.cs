namespace Portunus;

/// <summary>
/// The client's configuration keys of <c>07</c> section 4, each as <c>07</c> spells it, which
/// a refusal of the client's configuration names (<c>04</c> PRT-LIB-003).
/// </summary>
internal static class ClientKeys
{
    /// <summary>The section the client's keys are bound from.</summary>
    internal const string Section = "Portunus:Client";

    /// <summary>Where the client reads: <c>agent</c> or <c>file</c>.</summary>
    internal const string Source = "Portunus:Client:Source";

    /// <summary>The agent's socket.</summary>
    internal const string Socket = "Portunus:Client:Socket";

    /// <summary>The development file.</summary>
    internal const string File = "Portunus:Client:File";

    /// <summary>The bound of one exchange with the agent.</summary>
    internal const string Timeout = "Portunus:Client:Timeout";

    /// <summary>How long a read waits for the agent.</summary>
    internal const string WaitTimeout = "Portunus:Client:WaitTimeout";

    /// <summary>The pause between two asks while a read waits.</summary>
    internal const string PollInterval = "Portunus:Client:PollInterval";
}