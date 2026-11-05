using System;

namespace Portunus.Core;

/// <summary>
/// A version's number as the layout writes it (<c>03</c> PRT-VLT-004): 1 to 9999 in decimal
/// ASCII digits, without leading zeros, in a label's suffix and in a <c>.current</c> value alike.
/// </summary>
internal static class VersionNumber
{
    /// <summary>Reads <paramref name="digits"/>, or answers none where it is not a number of the rule.</summary>
    internal static int? Parse(ReadOnlySpan<char> digits)
    {
        if (digits.IsEmpty || digits.Length > 4 || digits[0] == '0')
        {
            return null;
        }

        int number = 0;
        foreach (char digit in digits)
        {
            if (digit is < '0' or > '9')
            {
                return null;
            }

            number = (number * 10) + (digit - '0');
        }

        return number <= Limits.VersionNumber ? number : null;
    }

    /// <summary>Reads <paramref name="digits"/>, the UTF-8 text of a value, as <see cref="Parse(ReadOnlySpan{char})"/> does.</summary>
    internal static int? Parse(ReadOnlySpan<byte> digits)
    {
        Span<char> text = stackalloc char[4];
        if (digits.Length > text.Length)
        {
            return null;
        }

        for (int index = 0; index < digits.Length; index++)
        {
            text[index] = (char)digits[index];
        }

        return Parse(text[..digits.Length]);
    }
}