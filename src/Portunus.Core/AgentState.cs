using System.Text.Json.Serialization;

namespace Portunus.Core;

/// <summary>
/// The state of an agent (<c>01</c> PRT-AGT-005): waiting, holding no set, or ready, holding one.
/// </summary>
public enum AgentState
{
    /// <summary>The agent holds no set, for the reason it gives.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    /// <summary>The agent holds a set and answers reads from it.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,
}