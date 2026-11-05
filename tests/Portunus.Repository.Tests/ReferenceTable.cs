using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// The tables of <c>07</c>, read from the specification: the codes of section 1, the
/// enumerations of section 2 with their members, in order, and each member's spelling, and
/// the configuration keys of section 4.
/// </summary>
internal static partial class ReferenceTable
{
    private static readonly string Reference = RepositoryRoot.Combine("docs", "spec", "07-reference.md");

    /// <summary>Every code of section 1.</summary>
    internal static FrozenSet<string> Codes { get; } = ReadCodes(Reference);

    /// <summary>Each enumeration of section 2, by name, with its members, in order, as name and spelling.</summary>
    internal static FrozenDictionary<string, IReadOnlyList<(string Name, string Spelling)>> Enumerations { get; } = ReadEnumerations(Reference);

    /// <summary>
    /// Every configuration key of section 4, a segment written <c>&lt;...&gt;</c> standing for
    /// any one segment, and a row's keys "alike for" other libraries written out for each.
    /// </summary>
    internal static FrozenSet<string> Keys { get; } = ReadKeys(Reference);

    internal static FrozenSet<string> ReadCodes(string reference)
    {
        return Section(reference, "## 1. ")
            .Select(line => CodeRowPattern().Match(line))
            .Where(row => row.Success)
            .Select(row => row.Groups["code"].Value)
            .ToFrozenSet(StringComparer.Ordinal);
    }

    internal static FrozenDictionary<string, IReadOnlyList<(string Name, string Spelling)>> ReadEnumerations(string reference)
    {
        Dictionary<string, IReadOnlyList<(string Name, string Spelling)>> enumerations = new(StringComparer.Ordinal);
        foreach (string line in Section(reference, "## 2. "))
        {
            Match row = EnumerationRowPattern().Match(line);
            if (row.Success)
            {
                enumerations[row.Groups["name"].Value] = [.. MemberPattern().Matches(row.Groups["members"].Value).Select(member => (member.Groups["name"].Value, member.Groups["spelling"].Value))];
            }
        }

        return enumerations.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static FrozenSet<string> ReadKeys(string reference)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (string line in Section(reference, "## 4. "))
        {
            Match row = KeyRowPattern().Match(line);
            if (!row.Success)
            {
                continue;
            }

            string cell = row.Groups["keys"].Value;
            string[] written = [.. KeyPattern().Matches(cell).Select(key => key.Groups["key"].Value)];
            Match alike = AlikePattern().Match(cell);
            string[] libraries = alike.Success ? [alike.Groups["first"].Value, alike.Groups["second"].Value] : [];
            foreach (string key in written)
            {
                keys.Add(key);
                string library = key.Split(':')[1];
                foreach (string other in libraries)
                {
                    keys.Add(key.Replace(":" + library + ":", ":" + other + ":", StringComparison.Ordinal));
                }
            }
        }

        return keys.ToFrozenSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Section(string reference, string heading)
    {
        bool inSection = false;
        foreach (string line in File.ReadLines(reference))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = line.StartsWith(heading, StringComparison.Ordinal);
                continue;
            }

            if (inSection)
            {
                yield return line;
            }
        }
    }

    [GeneratedRegex(@"^\| `(?<code>secrets\.[a-z.]+)`")]
    private static partial Regex CodeRowPattern();

    [GeneratedRegex(@"^\| `(?<name>[A-Z][A-Za-z]+)` \| (?<members>`[A-Z].*) \|$")]
    private static partial Regex EnumerationRowPattern();

    [GeneratedRegex(@"`(?<name>[A-Z][A-Za-z]+)` \(`(?<spelling>[a-z]+)`\)")]
    private static partial Regex MemberPattern();

    [GeneratedRegex(@"^\| (?<keys>`Portunus:[^|]+) \|")]
    private static partial Regex KeyRowPattern();

    [GeneratedRegex(@"`(?<key>Portunus(?::[^`:]+)+)`")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"alike for (?<first>[A-Z][a-z]+) and (?<second>[A-Z][a-z]+)")]
    private static partial Regex AlikePattern();
}