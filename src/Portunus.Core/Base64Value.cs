using System;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Portunus.Core;

/// <summary>
/// A value in a wire format: base64url of RFC 4648 section 5, unpadded, which JSON never
/// escapes (<c>06</c> PRT-ENG-006, P-023), encoded and decoded through pinned arrays alone
/// (PRT-ENG-005).
/// </summary>
internal static class Base64Value
{
    /// <summary>
    /// Writes <paramref name="value"/> as a string through <paramref name="writer"/>, encoded
    /// first into a pinned array, which is zeroed once written.
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        byte[] encoded = GC.AllocateArray<byte>(Base64Url.GetEncodedLength(value.Length), pinned: true);
        try
        {
            OperationStatus status = Base64Url.EncodeToUtf8(value, encoded, out _, out int written);
            if (status != OperationStatus.Done || written != encoded.Length)
            {
                throw new InvalidOperationException("A value could not be encoded in base64url.");
            }

            writer.WriteStringValue(encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    /// <summary>
    /// Decodes <paramref name="text"/>, the UTF-8 bytes of a base64url string, into an array of
    /// <paramref name="values"/>; answers false where it is not base64url of the rule: a
    /// character outside its alphabet, padding, a length no encoding has, or bits left over
    /// that are not zero.
    /// </summary>
    internal static bool TryDecode(ReadOnlySpan<byte> text, PinnedValues values, out ReadOnlyMemory<byte> value)
    {
        ArgumentNullException.ThrowIfNull(values);
        value = ReadOnlyMemory<byte>.Empty;
        int remainder = text.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        foreach (byte character in text)
        {
            if (Sextet(character) < 0)
            {
                return false;
            }
        }

        // The last character of a partial block carries bits beyond the value, which the one
        // exact form leaves zero.
        if (remainder != 0 && (Sextet(text[^1]) & (remainder == 2 ? 0x0F : 0x03)) != 0)
        {
            return false;
        }

        int length = (text.Length / 4 * 3) + (remainder == 0 ? 0 : remainder - 1);
        byte[] decoded = values.Allocate(length);
        OperationStatus status = Base64Url.DecodeFromUtf8(text, decoded, out int consumed, out int written);
        if (status != OperationStatus.Done || consumed != text.Length || written != length)
        {
            return false;
        }

        value = decoded;
        return true;
    }

    /// <summary>The six bits a character of the base64url alphabet stands for, or -1 for any other character.</summary>
    private static int Sextet(byte character)
    {
        return character switch
        {
            >= (byte)'A' and <= (byte)'Z' => character - 'A',
            >= (byte)'a' and <= (byte)'z' => character - 'a' + 26,
            >= (byte)'0' and <= (byte)'9' => character - '0' + 52,
            (byte)'-' => 62,
            (byte)'_' => 63,
            _ => -1,
        };
    }
}