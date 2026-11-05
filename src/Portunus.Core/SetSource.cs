using System.Text.Json.Serialization;

namespace Portunus.Core;

/// <summary>
/// Where a ready agent's set came from (<c>01</c> PRT-AGT-005).
/// </summary>
public enum SetSource
{
    /// <summary>The agent's fetch from the vault.</summary>
    [JsonStringEnumMemberName("vault")]
    Vault,

    /// <summary>A push from the owner's computer.</summary>
    [JsonStringEnumMemberName("push")]
    Push,
}