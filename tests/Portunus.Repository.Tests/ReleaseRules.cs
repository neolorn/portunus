using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>The release a change needs under Semantic Versioning (<c>04</c> PRT-LIB-007).</summary>
internal enum ReleaseNeed
{
    Patch,
    Minor,
    Major,
}

/// <summary>What changed since the newest final release: each break, which needs a major release, and each addition, which needs a minor one.</summary>
internal sealed record ReleaseComparison(IReadOnlyList<string> Breaks, IReadOnlyList<string> Additions)
{
    internal ReleaseNeed Need => Breaks.Count > 0 ? ReleaseNeed.Major : Additions.Count > 0 ? ReleaseNeed.Minor : ReleaseNeed.Patch;
}

/// <summary>
/// The release gate's rules (<c>04</c> PRT-LIB-007, <c>06</c> PRT-ENG-012): the public surface,
/// the codes, the configuration keys, the enumerations, the wire formats and the executables'
/// commands and options compared with the newest final release's, an addition minor and a
/// change or a removal major; and the version a release tag or a pull request must carry for
/// what changed.
/// </summary>
internal static partial class ReleaseRules
{
    /// <summary>
    /// Compares the surface and set of the newest final release with the current ones, each
    /// surface entry written as <c>Package: entry</c> of its public API files.
    /// </summary>
    internal static ReleaseComparison Compare(IReadOnlyCollection<string> baselineSurface, IReadOnlyCollection<string> surface, ReleaseSet baseline, ReleaseSet current)
    {
        ArgumentNullException.ThrowIfNull(baselineSurface);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        List<string> breaks = [];
        List<string> additions = [];

        breaks.AddRange(baselineSurface.Except(surface, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(entry => $"the public member {entry} is changed or removed"));
        additions.AddRange(surface.Except(baselineSurface, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(entry => $"the public member {entry} is added"));
        Listed("code", baseline.Codes, current.Codes, breaks, additions);
        Listed("configuration key", baseline.ConfigurationKeys, current.ConfigurationKeys, breaks, additions);
        foreach (string enumeration in baseline.Enumerations.Keys.Union(current.Enumerations.Keys).Order(StringComparer.Ordinal))
        {
            IReadOnlyDictionary<string, string> before = baseline.Enumerations.GetValueOrDefault(enumeration) ?? new Dictionary<string, string>();
            IReadOnlyDictionary<string, string> after = current.Enumerations.GetValueOrDefault(enumeration) ?? new Dictionary<string, string>();
            Named($"the value {enumeration}.", before, after, breaks, additions);
        }

        Listed("wire format member", baseline.WireFormats, current.WireFormats, breaks, additions);
        Listed("command or option", baseline.Commands, current.Commands, breaks, additions);
        return new ReleaseComparison(breaks, additions);
    }

    /// <summary>The problems of the release tag <paramref name="tag"/>, whose version must be at least what the changes since the final release <paramref name="baseline"/> need; a release candidate is held to what its final release needs.</summary>
    internal static IReadOnlyList<string> TagViolations(string tag, string baseline, ReleaseComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        (BigInteger Major, BigInteger Minor, BigInteger Patch) version = Core(tag);
        (BigInteger Major, BigInteger Minor, BigInteger Patch) before = Core(baseline);
        bool enough = comparison.Need switch
        {
            ReleaseNeed.Major => version.Major > before.Major,
            ReleaseNeed.Minor => version.Major > before.Major || (version.Major == before.Major && version.Minor > before.Minor),
            _ => version.CompareTo(before) > 0,
        };
        if (enough)
        {
            return [];
        }

        string need = comparison.Need switch { ReleaseNeed.Major => "major", ReleaseNeed.Minor => "minor", _ => "patch" };
        string changes = comparison.Need == ReleaseNeed.Patch ? "nothing changed, so it must be above it" : string.Join("; ", comparison.Need == ReleaseNeed.Major ? comparison.Breaks : comparison.Additions);
        return [$"{tag} is below the {need} release the changes since {baseline} need: {changes}"];
    }

    /// <summary>The problems of a pull request, whose version is a pre-release: a change that needs a major release where no commit since the final release <paramref name="baseline"/> is marked breaking (<paramref name="marked"/>).</summary>
    internal static IReadOnlyList<string> PullRequestViolations(bool marked, string baseline, ReleaseComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (comparison.Need != ReleaseNeed.Major || marked)
        {
            return [];
        }

        return [.. comparison.Breaks.Select(change => $"{change} since {baseline}, and no commit since then carries ! or BREAKING CHANGE:")];
    }

    /// <summary>Whether a commit's message marks it breaking: a <c>!</c> before its header's colon, or a <c>BREAKING CHANGE:</c> footer line (<c>06</c> PRT-ENG-011).</summary>
    internal static bool MarkedBreaking(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        string[] lines = message.ReplaceLineEndings("\n").Split('\n');
        return BreakingHeader().IsMatch(lines[0]) || lines.Skip(1).Any(line => line.StartsWith("BREAKING CHANGE: ", StringComparison.Ordinal));
    }

    /// <summary>The core version <c>X.Y.Z</c> of the release tag <paramref name="tag"/>, a release candidate's included.</summary>
    internal static (BigInteger Major, BigInteger Minor, BigInteger Patch) Core(string tag)
    {
        Match release = ReleaseTag().Match(tag);
        if (!release.Success)
        {
            throw new ArgumentException($"{tag} is no release tag.", nameof(tag));
        }

        return (Number(release, "major"), Number(release, "minor"), Number(release, "patch"));
    }

    /// <summary>Whether <paramref name="tag"/> is a release tag: <c>vX.Y.Z</c> or <c>vX.Y.Z-rc.N</c>, N from 1 (P-017).</summary>
    internal static bool IsReleaseTag(string tag)
    {
        return ReleaseTag().IsMatch(tag);
    }

    private static BigInteger Number(Match release, string group)
    {
        return BigInteger.Parse(release.Groups[group].Value, CultureInfo.InvariantCulture);
    }

    private static void Listed(string kind, IReadOnlyCollection<string> before, IReadOnlyCollection<string> after, List<string> breaks, List<string> additions)
    {
        breaks.AddRange(before.Except(after, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => $"the {kind} {name} is removed"));
        additions.AddRange(after.Except(before, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => $"the {kind} {name} is added"));
    }

    private static void Named(string prefix, IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, List<string> breaks, List<string> additions)
    {
        foreach (KeyValuePair<string, string> named in before.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!after.TryGetValue(named.Key, out string? now))
            {
                breaks.Add($"{prefix}{named.Key} is removed");
            }
            else if (!string.Equals(now, named.Value, StringComparison.Ordinal))
            {
                breaks.Add($"{prefix}{named.Key} spelt {named.Value} is now spelt {now}");
            }
        }

        additions.AddRange(after.Keys.Except(before.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => $"{prefix}{name} is added"));
    }

    [GeneratedRegex(@"^v(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(-rc\.[1-9]\d*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseTag();

    [GeneratedRegex(@"^[a-z]+(\([^)]*\))?!: ", RegexOptions.CultureInvariant)]
    private static partial Regex BreakingHeader();
}