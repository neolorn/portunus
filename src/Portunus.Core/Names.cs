using System.Text.RegularExpressions;

namespace Portunus.Core;

/// <summary>
/// The rules of the names the library reads (<c>00</c> section 1.5): an instance's or a
/// process's name, a vault title (<c>03</c> PRT-VLT-001), an item title (PRT-VLT-002) and a
/// secret name (PRT-VLT-003, PRT-VLT-004). Each is ASCII, so its length in characters is its
/// length in Unicode scalar values.
/// </summary>
internal static partial class Names
{
    /// <summary>The longest name, item title and secret name.</summary>
    internal const int MaximumLength = 64;

    /// <summary>Whether <paramref name="name"/> is 1 to 64 lower-case ASCII letters and digits separated by single <c>-</c>, starting with a letter.</summary>
    internal static bool IsName(string? name)
    {
        return name is { Length: > 0 and <= MaximumLength } && NamePattern().IsMatch(name);
    }

    /// <summary>Whether <paramref name="title"/> is two words separated by one space, each 1 to 32 ASCII letters, digits and <c>-</c>, starting with a letter.</summary>
    internal static bool IsVaultTitle(string? title)
    {
        return title is not null && VaultTitlePattern().IsMatch(title);
    }

    /// <summary>
    /// Whether <paramref name="title"/> is 1 to 64 ASCII letters, digits, spaces, <c>-</c>,
    /// <c>_</c> and <c>.</c>, starting with a letter, with no space at its end and no two in a row.
    /// </summary>
    internal static bool IsItemTitle(string? title)
    {
        return title is { Length: > 0 and <= MaximumLength } && ItemTitlePattern().IsMatch(title);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is 1 to 64 ASCII letters, digits, <c>.</c>, <c>-</c> and
    /// <c>_</c>, starting with a letter, with no two <c>.</c> in a row and none at its end, and
    /// not ending in <c>.</c> and digits, <c>.current</c> or <c>.retired</c>, which mark versions.
    /// The suffixes are compared without regard to the case of ASCII letters, as names are.
    /// </summary>
    internal static bool IsSecretName(string? name)
    {
        return name is { Length: > 0 and <= MaximumLength } && SecretNamePattern().IsMatch(name) && !VersionMarkPattern().IsMatch(name);
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9]*(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9-]{0,31} [A-Za-z][A-Za-z0-9-]{0,31}\z", RegexOptions.CultureInvariant)]
    private static partial Regex VaultTitlePattern();

    [GeneratedRegex(@"\A[A-Za-z](?:[A-Za-z0-9_.-]| (?=[A-Za-z0-9_.-]))*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ItemTitlePattern();

    [GeneratedRegex(@"\A[A-Za-z](?:[A-Za-z0-9_-]|\.(?=[A-Za-z0-9_-]))*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNamePattern();

    [GeneratedRegex(@"\.(?:[0-9]+|current|retired)\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex VersionMarkPattern();
}