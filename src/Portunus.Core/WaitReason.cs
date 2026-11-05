using System.Text.Json.Serialization;

namespace Portunus.Core;

/// <summary>
/// Why a waiting agent holds no set (<c>01</c> PRT-AGT-004, PRT-AGT-005).
/// </summary>
public enum WaitReason
{
    /// <summary>The agent is reading the vault.</summary>
    [JsonStringEnumMemberName("fetching")]
    Fetching,

    /// <summary>The last attempt failed for any reason but a rate limit or a refused token, a timeout included.</summary>
    [JsonStringEnumMemberName("unreachable")]
    Unreachable,

    /// <summary>The vault refused the last attempt for its rate limit.</summary>
    [JsonStringEnumMemberName("ratelimited")]
    RateLimited,

    /// <summary>The vault refused the agent's token at the last attempt: revoked, or not granted the vault.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,

    /// <summary>The vault's set breaks the layout's rule, and the agent waits for a push or a restart.</summary>
    [JsonStringEnumMemberName("layoutinvalid")]
    LayoutInvalid,

    /// <summary>The agent takes its set from a push alone, and none has come.</summary>
    [JsonStringEnumMemberName("awaitingpush")]
    AwaitingPush,
}