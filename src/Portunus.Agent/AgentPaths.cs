namespace Portunus.Agent;

/// <summary>
/// The system paths the agent's start reads (P-027): the directory of the agents' files and the
/// kernel's list of swap areas. The shipped entry point passes <see cref="System"/> alone; a
/// test gives others through <c>InternalsVisibleTo</c>.
/// </summary>
/// <param name="ConfigurationDirectory">The directory holding <c>&lt;instance&gt;.json</c>.</param>
/// <param name="Swaps">The list of active swap areas.</param>
internal sealed record AgentPaths(string ConfigurationDirectory, string Swaps)
{
    /// <summary>The real paths, <c>/etc/portunus</c> and <c>/proc/swaps</c> (<c>07</c> section 7).</summary>
    internal static AgentPaths System { get; } = new("/etc/portunus", "/proc/swaps");
}