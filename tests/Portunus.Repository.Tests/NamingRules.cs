using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// The names of <c>06</c> PRT-ENG-007 a built assembly holds: each code it answers is one of
/// <c>07</c> section 1, each configuration key it binds is one of section 4, and each
/// enumeration is one of section 2, its members named, ordered and spelled as the section
/// gives them (<c>04</c> PRT-LIB-002).
/// </summary>
internal static partial class NamingRules
{
    /// <summary>Answers each string of <paramref name="assembly"/> that has a code's shape and is no code of <c>07</c> section 1.</summary>
    internal static IEnumerable<string> CodeViolations(string assembly, IEnumerable<string> strings)
    {
        return strings.Where(text => CodePattern().IsMatch(text) && !ReferenceTable.Codes.Contains(text))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(text => $"{assembly} answers the code {text}, which 07 section 1 does not list.");
    }

    /// <summary>
    /// Answers each string of <paramref name="assembly"/> that has a configuration key's shape
    /// and is neither a key of <c>07</c> section 4 nor a section that holds one; a segment
    /// <c>07</c> writes <c>&lt;...&gt;</c> stands for any one segment.
    /// </summary>
    internal static IEnumerable<string> KeyViolations(string assembly, IEnumerable<string> strings)
    {
        return strings.Where(text => KeyPattern().IsMatch(text) && !ReferenceTable.Keys.Any(key => IsKeyOrSection(text, key)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(text => $"{assembly} binds the configuration key {text}, which 07 section 4 does not list.");
    }

    /// <summary>
    /// Answers each public enumeration of <paramref name="enumerations"/> that <c>07</c> section 2
    /// does not give, and each whose members are not those it gives, in its order, each carrying
    /// its spelling.
    /// </summary>
    internal static IEnumerable<string> SpellingViolations(IReadOnlyDictionary<string, IReadOnlyList<(string Name, string? Spelling)>> enumerations)
    {
        foreach ((string type, IReadOnlyList<(string Name, string? Spelling)> members) in enumerations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            string name = type[(type.LastIndexOf('.') + 1)..];
            if (!ReferenceTable.Enumerations.TryGetValue(name, out IReadOnlyList<(string Name, string Spelling)>? given))
            {
                yield return $"{type} is an enumeration 07 section 2 does not give.";
                continue;
            }

            string declared = string.Join(", ", members.Select(member => $"{member.Name} ({member.Spelling ?? "no spelling"})"));
            string expected = string.Join(", ", given.Select(member => $"{member.Name} ({member.Spelling})"));
            if (!string.Equals(declared, expected, StringComparison.Ordinal))
            {
                yield return $"{type} declares {declared}, not {expected} as 07 section 2 gives them.";
            }
        }
    }

    /// <summary>A string of a code's shape: <c>secrets.</c> and lower-case words separated by dots.</summary>
    [GeneratedRegex(@"\Asecrets\.[a-z]+(?:\.[a-z]+)*\z")]
    private static partial Regex CodePattern();

    /// <summary>Whether <paramref name="text"/> is <paramref name="key"/>, or a section above it, segment by segment.</summary>
    private static bool IsKeyOrSection(string text, string key)
    {
        string[] segments = text.Split(':');
        string[] given = key.Split(':');
        return segments.Length <= given.Length
            && segments.Select((segment, index) => given[index].StartsWith('<') || string.Equals(segment, given[index], StringComparison.Ordinal)).All(alike => alike);
    }

    /// <summary>A string of a configuration key's shape: <c>Portunus</c> and segments separated by colons.</summary>
    [GeneratedRegex(@"\APortunus(?::[A-Za-z0-9.]+)+\z")]
    private static partial Regex KeyPattern();
}