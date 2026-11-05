using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Portunus.Core;

/// <summary>
/// The exception in which a "(startup)" code of the library's reference is thrown while the
/// host starts: a configuration value outside its rule, the development file outside
/// Development, or a development file that cannot be read stops the start before any secret
/// is read (<c>04</c> PRT-LIB-003).
/// </summary>
public sealed class SecretsStartupException : Exception
{
    /// <summary>Makes the exception for <paramref name="code"/> with its <paramref name="details"/>.</summary>
    /// <param name="code">The startup code, such as <c>secrets.startup.configurationinvalid</c>.</param>
    /// <param name="details">The code's details, such as <c>key</c>; never a value or a token.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="details"/> is null.</exception>
    public SecretsStartupException(string code, IReadOnlyDictionary<string, string> details)
        : base(Describe(code, details))
    {
        Code = code;
        Details = details.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The startup code.</summary>
    public string Code { get; }

    /// <summary>The code's details, by name.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    private static string Describe(string code, IReadOnlyDictionary<string, string> details)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(details);
        string named = string.Join(", ", details.OrderBy(detail => detail.Key, StringComparer.Ordinal).Select(detail => $"{detail.Key} {detail.Value}"));
        return details.Count == 0 ? $"The host did not start: {code}." : $"The host did not start: {code} ({named}).";
    }
}