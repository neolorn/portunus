using System;
using System.Buffers;
using System.Buffers.Text;

namespace Portunus.Core;

/// <summary>
/// A value in a wire format: base64 of RFC 4648 section 4, padded, with no whitespace
/// (<c>06</c> PRT-ENG-006), decoded into a pinned array (PRT-ENG-005).
/// </summary>
internal static class Base64Value
{
    /// <summary>
    /// Decodes <paramref name="text"/>, the UTF-8 bytes of a base64 string, into an array of
    /// <paramref name="values"/>; answers false where it is not base64 of the rule.
    /// </summary>
    internal static bool TryDecode(ReadOnlySpan<byte> text, PinnedValues values, out ReadOnlyMemory<byte> value)
    {
        ArgumentNullException.ThrowIfNull(values);
        value = ReadOnlyMemory<byte>.Empty;
        if (text.Length % 4 != 0)
        {
            return false;
        }

        int padding = text.EndsWith("=="u8) ? 2 : text.EndsWith("="u8) ? 1 : 0;
        for (int index = 0; index < text.Length - padding; index++)
        {
            if (!IsAlphabet(text[index]))
            {
                return false;
            }
        }

        int length = (text.Length / 4 * 3) - padding;
        byte[] decoded = values.Allocate(length);
        OperationStatus status = Base64.DecodeFromUtf8(text, decoded, out int consumed, out int written);
        if (status != OperationStatus.Done || consumed != text.Length || written != length)
        {
            return false;
        }

        value = decoded;
        return true;
    }

    private static bool IsAlphabet(byte character)
    {
        return character is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'+' or (byte)'/';
    }
}