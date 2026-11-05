using System;
using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Portunus.Hermes;

/// <summary>
/// A value the table calls base64 (<c>03</c> PRT-VLT-003): RFC 4648 section 4, padded, with
/// no white space, decoded from its UTF-8 text into a pinned array; anything else is not of
/// its form.
/// </summary>
internal static class Base64Text
{
    /// <summary>The decoded bytes in a pinned array, or none where <paramref name="text"/> is not of the form.</summary>
    internal static byte[]? Decode(ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty || text.Length % 4 != 0 || text.IndexOfAnyExcept(Alphabet) >= 0)
        {
            return null;
        }

        byte[] decoded = GC.AllocateArray<byte>(Base64.GetMaxDecodedFromUtf8Length(text.Length), pinned: true);
        try
        {
            if (Base64.DecodeFromUtf8(text, decoded, out int consumed, out int written) != OperationStatus.Done || consumed != text.Length)
            {
                return null;
            }

            byte[] value = GC.AllocateArray<byte>(written, pinned: true);
            decoded.AsSpan(0, written).CopyTo(value);
            return value;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    private static SearchValues<byte> Alphabet { get; } = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="u8);
}