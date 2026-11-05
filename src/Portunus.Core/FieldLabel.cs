using System;
using System.Text.RegularExpressions;

namespace Portunus.Core;

/// <summary>
/// A field's label read by the layout's rule (<c>03</c> PRT-VLT-003, PRT-VLT-004,
/// PRT-VLT-005): the secret's name, what the field is to it, and a version's number.
/// </summary>
/// <param name="Name">The secret's name, the label without its suffix.</param>
/// <param name="Kind">What the field is to its secret.</param>
/// <param name="Number">A version's number, 1 to 9999; 0 for a single secret and the current marker.</param>
internal readonly partial record struct FieldLabel(string Name, FieldKind Kind, int Number)
{
    /// <summary>Reads <paramref name="label"/>, or answers none where it is outside the rule, a version's number included.</summary>
    internal static FieldLabel? Parse(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        Match current = CurrentPattern().Match(label);
        if (current.Success)
        {
            return Named(current.Groups["name"].Value, FieldKind.Current, 0);
        }

        Match retired = RetiredPattern().Match(label);
        if (retired.Success)
        {
            return VersionNumber.Parse(retired.Groups["number"].ValueSpan) is int number ? Named(retired.Groups["name"].Value, FieldKind.Retired, number) : null;
        }

        Match version = VersionPattern().Match(label);
        if (version.Success)
        {
            return VersionNumber.Parse(version.Groups["number"].ValueSpan) is int number ? Named(version.Groups["name"].Value, FieldKind.Version, number) : null;
        }

        return Named(label, FieldKind.Single, 0);
    }

    private static FieldLabel? Named(string name, FieldKind kind, int number)
    {
        return Names.IsSecretName(name) ? new FieldLabel(name, kind, number) : null;
    }

    [GeneratedRegex(@"\A(?<name>.+)\.current\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CurrentPattern();

    [GeneratedRegex(@"\A(?<name>.+)\.(?<number>[0-9]+)\.retired\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RetiredPattern();

    [GeneratedRegex(@"\A(?<name>.+)\.(?<number>[0-9]+)\z", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex VersionPattern();
}