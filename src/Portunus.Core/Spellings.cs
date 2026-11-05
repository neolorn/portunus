using System;

namespace Portunus.Core;

/// <summary>
/// The spelling of each enumeration's member in every wire format and log, as <c>07</c>
/// section 2 gives it and as each member's <c>JsonStringEnumMemberName</c> carries it.
/// </summary>
internal static class Spellings
{
    internal static string Of(AgentState state)
    {
        return state switch
        {
            AgentState.Waiting => "waiting",
            AgentState.Ready => "ready",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "An agent state of 07 section 2."),
        };
    }

    internal static string Of(WaitReason reason)
    {
        return reason switch
        {
            WaitReason.Fetching => "fetching",
            WaitReason.Unreachable => "unreachable",
            WaitReason.RateLimited => "ratelimited",
            WaitReason.Refused => "refused",
            WaitReason.LayoutInvalid => "layoutinvalid",
            WaitReason.AwaitingPush => "awaitingpush",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A wait reason of 07 section 2."),
        };
    }

    internal static string Of(SetSource source)
    {
        return source switch
        {
            SetSource.Vault => "vault",
            SetSource.Push => "push",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A set source of 07 section 2."),
        };
    }

    internal static AgentState? ReadAgentState(string? spelling)
    {
        return spelling switch
        {
            "waiting" => AgentState.Waiting,
            "ready" => AgentState.Ready,
            _ => null,
        };
    }

    internal static WaitReason? ReadWaitReason(string? spelling)
    {
        return spelling switch
        {
            "fetching" => WaitReason.Fetching,
            "unreachable" => WaitReason.Unreachable,
            "ratelimited" => WaitReason.RateLimited,
            "refused" => WaitReason.Refused,
            "layoutinvalid" => WaitReason.LayoutInvalid,
            "awaitingpush" => WaitReason.AwaitingPush,
            _ => null,
        };
    }

    internal static SetSource? ReadSetSource(string? spelling)
    {
        return spelling switch
        {
            "vault" => SetSource.Vault,
            "push" => SetSource.Push,
            _ => null,
        };
    }
}