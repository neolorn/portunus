using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Portunus.Repository.Tests;

/// <summary>The release gate's comparison and its version rule (<c>04</c> PRT-LIB-007).</summary>
[Trait("Category", "unit")]
public sealed class ReleaseRuleTests
{
    private static readonly string[] Surface =
    [
        "Portunus.Core: Portunus.Core.ISecretClient",
        "Portunus.Core: Portunus.Core.ISecretClient.StatusAsync(System.Threading.CancellationToken cancellationToken) -> System.Threading.Tasks.ValueTask<Portunus.Core.Result<Portunus.Core.AgentStatus!>!>",
        "Portunus.Core: Portunus.Core.Secret",
    ];

    private static readonly ReleaseSet Baseline = new(
        ["secrets.secret.denied", "secrets.secret.unknown"],
        ["Portunus:Client:Socket", "Portunus:Client:Timeout"],
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["AgentState"] = new Dictionary<string, string> { ["Waiting"] = "waiting", ["Ready"] = "ready" },
        },
        ["RequestFormat address", "RequestFormat kind"],
        ["portunus push", "portunus --vault"]);

    [Theory]
    [InlineData("member removed")]
    [InlineData("member changed")]
    [InlineData("code removed")]
    [InlineData("key removed")]
    [InlineData("spelling changed")]
    [InlineData("enumeration member removed")]
    [InlineData("enumeration removed")]
    [InlineData("wire format member removed")]
    [InlineData("command removed")]
    [InlineData("option removed")]
    public void PRT_LIB_007_AC1_ChangeOrRemovalOfTheSurfaceCodesKeysEnumerationsFormatsOrCommandsNeedsAMajorRelease(string change)
    {
        (string[] surface, ReleaseSet current) = Changed(change);

        ReleaseComparison comparison = ReleaseRules.Compare(Surface, surface, Baseline, current);

        Assert.Equal(ReleaseNeed.Major, comparison.Need);
        Assert.NotEmpty(comparison.Breaks);
    }

    [Theory]
    [InlineData("member added")]
    [InlineData("code added")]
    [InlineData("key added")]
    [InlineData("enumeration member added")]
    [InlineData("enumeration added")]
    [InlineData("wire format member added")]
    [InlineData("command added")]
    [InlineData("option added")]
    public void PRT_LIB_007_AC1_AdditionOfAMemberCodeKeySpellingFormatMemberOrCommandNeedsAMinorRelease(string change)
    {
        (string[] surface, ReleaseSet current) = Changed(change);

        ReleaseComparison comparison = ReleaseRules.Compare(Surface, surface, Baseline, current);

        Assert.Equal(ReleaseNeed.Minor, comparison.Need);
        Assert.Empty(comparison.Breaks);
        Assert.NotEmpty(comparison.Additions);
    }

    [Fact]
    public void Compare_NothingChanged_NeedsAPatchRelease()
    {
        ReleaseComparison comparison = ReleaseRules.Compare(Surface, Surface, Baseline, Baseline);

        Assert.Equal(ReleaseNeed.Patch, comparison.Need);
        Assert.Empty(comparison.Breaks);
        Assert.Empty(comparison.Additions);
    }

    [Fact]
    public void PRT_LIB_007_AC1_BeforeTheFirstFinalReleaseTheBaselineIsEmptySoNothingIsABreak()
    {
        ReleaseComparison comparison = ReleaseRules.Compare([], Surface, ReleaseSet.Empty, Baseline);

        Assert.Empty(comparison.Breaks);
    }

    [Theory]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v2.0.0", true)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v2.0.0-rc.1", true)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v1.1.0", false)]
    [InlineData(ReleaseNeedName.Major, "v1.0.0", "v1.1.0-rc.1", false)]
    [InlineData(ReleaseNeedName.Minor, "v1.0.0", "v1.1.0", true)]
    [InlineData(ReleaseNeedName.Minor, "v1.2.3", "v2.0.0", true)]
    [InlineData(ReleaseNeedName.Minor, "v1.0.0", "v1.0.1", false)]
    [InlineData(ReleaseNeedName.Minor, "v1.9.0", "v1.10.0-rc.2", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.1", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.1-rc.1", true)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.0-rc.2", false)]
    [InlineData(ReleaseNeedName.Patch, "v1.0.0", "v1.0.0", false)]
    public void PRT_LIB_007_AC1_ReleaseTagBelowWhatChangedNeedsFails(string need, string baseline, string tag, bool passes)
    {
        IReadOnlyList<string> violations = ReleaseRules.TagViolations(tag, baseline, Needing(need));

        Assert.Equal(passes, violations.Count == 0);
    }

    [Theory]
    [InlineData(ReleaseNeedName.Major, false, false)]
    [InlineData(ReleaseNeedName.Major, true, true)]
    [InlineData(ReleaseNeedName.Minor, false, true)]
    [InlineData(ReleaseNeedName.Patch, false, true)]
    public void PRT_LIB_007_AC2_PullRequestNeedingAMajorReleasePassesOnlyWhereACommitSinceTheNewestFinalReleaseIsMarkedBreaking(string need, bool marked, bool passes)
    {
        IReadOnlyList<string> violations = ReleaseRules.PullRequestViolations(marked, "v1.0.0", Needing(need));

        Assert.Equal(passes, violations.Count == 0);
    }

    [Theory]
    [InlineData("refactor(core)!: rename the agent's states", true)]
    [InlineData("feat!: drop the old option", true)]
    [InlineData("refactor(core): rename the agent's states\n\nBREAKING CHANGE: the states are renamed", true)]
    [InlineData("refactor(core): rename the agent's states\n\n- BREAKING CHANGE: is not a footer here", false)]
    [InlineData("feat(agent): add a code", false)]
    public void MarkedBreaking_CommitMessage_ReadsItsMarkOrFooter(string message, bool marked)
    {
        Assert.Equal(marked, ReleaseRules.MarkedBreaking(message));
    }

    [Fact]
    public void ReleaseSet_WrittenAndParsed_IsTheSameSet()
    {
        ReleaseSet parsed = ReleaseSet.Parse(Baseline.Write());

        Assert.Equal(Baseline.Write(), parsed.Write());
        Assert.EndsWith("}\n", Baseline.Write(), StringComparison.Ordinal);
    }

    [Fact]
    public void PRT_LIB_007_AC1_SetReadFromTheBuiltAssembliesHoldsTheCodesKeysEnumerationsFormatsAndCommands()
    {
        ReleaseSet set = ReleaseSet.Read();

        Assert.Contains("secrets.secret.denied", set.Codes);
        Assert.Contains("secrets.push.wrongvault", set.Codes);
        Assert.Contains("Portunus:Client:WaitTimeout", set.ConfigurationKeys);
        Assert.Contains("Portunus:Agent:PushGroup", set.ConfigurationKeys);
        Assert.Equal("awaitingpush", set.Enumerations["WaitReason"]["AwaitingPush"]);
        Assert.Contains("RequestFormat address", set.WireFormats);
        Assert.Contains("AnswerFormat loadedAt", set.WireFormats);
        Assert.Contains("PushAnswerFormat accepted", set.WireFormats);
        Assert.Contains("WireJson details", set.WireFormats);
        Assert.Equal(
            ["portunus --account", "portunus --instance", "portunus --replace", "portunus --to", "portunus --vault", "portunus check", "portunus push", "portunus status", "portunus-agent receive", "portunus-agent run", "portunus-agent status"],
            set.Commands.Order(StringComparer.Ordinal));
    }

    private static ReleaseComparison Needing(string need)
    {
        return Enum.Parse<ReleaseNeed>(need) switch
        {
            ReleaseNeed.Major => new ReleaseComparison(["Portunus.Core: Portunus.Core.Secret is removed"], []),
            ReleaseNeed.Minor => new ReleaseComparison([], ["Portunus.Core: Portunus.Core.Failure is added"]),
            _ => new ReleaseComparison([], []),
        };
    }

    private static (string[] Surface, ReleaseSet Current) Changed(string change)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> enumerations = new(Baseline.Enumerations);
        string[] surface = Surface;
        string[] codes = [.. Baseline.Codes];
        string[] keys = [.. Baseline.ConfigurationKeys];
        string[] formats = [.. Baseline.WireFormats];
        string[] commands = [.. Baseline.Commands];
        switch (change)
        {
            case "member removed":
                surface = [.. Surface.Where(entry => !entry.EndsWith(".Secret", StringComparison.Ordinal))];
                break;
            case "member changed":
                surface = [.. Surface.Select(entry => entry.Replace("AgentStatus!", "AgentStatus?", StringComparison.Ordinal))];
                break;
            case "member added":
                surface = [.. Surface, "Portunus.Core: Portunus.Core.Failure"];
                break;
            case "code removed":
                codes = ["secrets.secret.denied"];
                break;
            case "code added":
                codes = [.. codes, "secrets.secret.expired"];
                break;
            case "key removed":
                keys = ["Portunus:Client:Socket"];
                break;
            case "key added":
                keys = [.. keys, "Portunus:Client:Retries"];
                break;
            case "spelling changed":
                enumerations["AgentState"] = new Dictionary<string, string> { ["Waiting"] = "pending", ["Ready"] = "ready" };
                break;
            case "enumeration member removed":
                enumerations["AgentState"] = new Dictionary<string, string> { ["Ready"] = "ready" };
                break;
            case "enumeration member added":
                enumerations["AgentState"] = new Dictionary<string, string> { ["Waiting"] = "waiting", ["Ready"] = "ready", ["Stopping"] = "stopping" };
                break;
            case "enumeration removed":
                enumerations.Remove("AgentState");
                break;
            case "enumeration added":
                enumerations["SetSource"] = new Dictionary<string, string> { ["Vault"] = "vault" };
                break;
            case "wire format member removed":
                formats = ["RequestFormat kind"];
                break;
            case "wire format member added":
                formats = [.. formats, "RequestFormat deadline"];
                break;
            case "command removed":
                commands = ["portunus --vault"];
                break;
            case "command added":
                commands = [.. commands, "portunus rotate"];
                break;
            case "option removed":
                commands = ["portunus push"];
                break;
            default:
                commands = [.. commands, "portunus --profile"];
                break;
        }

        return (surface, new ReleaseSet(codes, keys, enumerations, formats, commands));
    }

    /// <summary>The names of <see cref="ReleaseNeed"/>, which a test's data can carry where the internal type cannot.</summary>
    private static class ReleaseNeedName
    {
        internal const string Major = nameof(ReleaseNeed.Major);

        internal const string Minor = nameof(ReleaseNeed.Minor);

        internal const string Patch = nameof(ReleaseNeed.Patch);
    }
}