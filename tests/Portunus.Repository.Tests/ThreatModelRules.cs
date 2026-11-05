using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// What <c>08</c> PRT-THR-001 criterion 1 reads: every requirement its table of threats and
/// controls names, and the requirements some test of the repository carries the identifier
/// of, save those on the list kept beside the test, each with its reason (P-041).
/// </summary>
internal static partial class ThreatModelRules
{
    /// <summary>The requirements the table names that carry no test of their own, each with its reason (P-041).</summary>
    internal static FrozenDictionary<string, string> Excluded { get; } = new Dictionary<string, string>
    {
        ["PRT-ENG-012"] = "the pipeline's own run proves it",
        ["PRT-OPS-005"] = "the integration tests' run checks it",
        ["PRT-THR-003"] = "the review checks it",
        ["PRT-OPS-002"] = "it carries no criteria of its own",
        ["PRT-PSH-001"] = "it carries no criteria of its own",
        ["PRT-ENG-005"] = "the tests of PRT-AGT-001 criterion 3 prove it",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The requirements the table of PRT-THR-001 names in <paramref name="model"/>, the text of <c>08</c>, PRT-THR-001 itself and those on the list left out.</summary>
    internal static IReadOnlyList<string> Named(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        int start = model.IndexOf("**PRT-THR-001**", StringComparison.Ordinal);
        int end = start < 0 ? -1 : model.IndexOf("**Acceptance criteria**", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException("The threat model holds no table of PRT-THR-001 before its acceptance criteria.");
        }

        return [.. RequirementPattern().Matches(model[start..end]).Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Where(requirement => requirement != "PRT-THR-001" && !Excluded.ContainsKey(requirement))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The requirements some test under <paramref name="tests"/> is named after, as
    /// <c>PRT-AGT-008</c> for <c>PRT_AGT_008_AC1_...</c>, read from every C# file but build
    /// output, its comments and string literals blanked so a probe's source held in a literal
    /// is no test of the repository's.
    /// </summary>
    internal static IReadOnlySet<string> Tested(string tests)
    {
        HashSet<string> tested = new(StringComparer.Ordinal);
        IEnumerable<string> files = Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"));
        foreach (string file in files)
        {
            string code = CSharpSource.Read(File.ReadAllText(file)).Code;
            foreach (Match test in TestPattern().Matches(code))
            {
                tested.Add(test.Groups["requirement"].Value.Replace('_', '-'));
            }
        }

        return tested;
    }

    /// <summary>Answers each requirement of <paramref name="named"/> no test of <paramref name="tested"/> carries.</summary>
    internal static IReadOnlyList<string> Untested(IEnumerable<string> named, IReadOnlySet<string> tested)
    {
        return [.. named.Where(requirement => !tested.Contains(requirement)).Select(requirement => $"{requirement} names a control of PRT-THR-001, and no test carries its identifier.")];
    }

    [GeneratedRegex(@"PRT-[A-Z]{3}-\d{3}")]
    private static partial Regex RequirementPattern();

    [GeneratedRegex(@"\[(?:Fact|Theory)\b[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+(?:async\s+)?[\w<>]+\s+(?<requirement>PRT_[A-Z]{3}_\d{3})_AC\d+_\w+\s*\(")]
    private static partial Regex TestPattern();
}