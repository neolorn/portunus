using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Portunus.Repository.Tests;

/// <summary>
/// The agent's unit and its drop-in under <c>deploy/</c> (<c>05</c> PRT-OPS-003): each holds
/// every line the paragraph "The unit's settings" of <c>07</c> section 7 gives it.
/// </summary>
internal static partial class DeployRules
{
    /// <summary>The unit's file and the drop-in's, by the name <c>07</c> gives them.</summary>
    internal const string Unit = "portunus-agent@.service";

    internal const string DropIn = "token.conf";

    /// <summary>Answers each line <c>07</c> section 7 gives the unit or the drop-in that the file under <c>deploy/</c> lacks.</summary>
    internal static IReadOnlyList<string> Violations(string root, string reference)
    {
        List<string> violations = [];
        foreach ((string file, IReadOnlyList<string> settings) in Settings(reference))
        {
            string path = Path.Combine(root, "deploy", file);
            HashSet<string> lines = File.Exists(path) ? [.. File.ReadLines(path).Select(line => line.Trim())] : [];
            violations.AddRange(settings.Where(setting => !lines.Contains(setting)).Select(setting => $"deploy/{file} lacks the line {setting}, which 07 section 7 gives it."));
        }

        return violations;
    }

    /// <summary>The settings <c>07</c> section 7 gives each file: each backticked <c>Key=Value</c> of its sentence.</summary>
    internal static IReadOnlyList<(string File, IReadOnlyList<string> Settings)> Settings(string reference)
    {
        string paragraph = string.Join(" ", File.ReadLines(reference).SkipWhile(line => !line.StartsWith("**The unit's settings**", StringComparison.Ordinal)).TakeWhile(line => line.Length > 0));
        int dropIn = paragraph.IndexOf("`" + DropIn + "`:", StringComparison.Ordinal);
        return
        [
            (Unit, [.. SettingPattern().Matches(paragraph[..dropIn]).Select(setting => setting.Groups["setting"].Value)]),
            (DropIn, [.. SettingPattern().Matches(paragraph[dropIn..]).Select(setting => setting.Groups["setting"].Value)]),
        ];
    }

    [GeneratedRegex(@"`(?<setting>[A-Za-z]+=[^`]+)`")]
    private static partial Regex SettingPattern();
}