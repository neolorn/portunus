using System;
using System.Globalization;

namespace Portunus.Core;

/// <summary>
/// An instant in a wire format: RFC 3339 with an offset of zero, to the second, as
/// <c>2026-11-05T09:00:00Z</c> (<c>06</c> PRT-ENG-006, <c>07</c> section 6).
/// </summary>
internal static class Instant
{
    private const string Form = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Writes <paramref name="instant"/> in the form, at an offset of zero, its fraction of a second left out.</summary>
    internal static string Format(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(Form, CultureInfo.InvariantCulture);
    }

    /// <summary>Reads <paramref name="text"/>, or answers none where it is not in the form.</summary>
    internal static DateTimeOffset? Parse(string text)
    {
        return DateTimeOffset.TryParseExact(text, Form, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset instant) ? instant : null;
    }
}