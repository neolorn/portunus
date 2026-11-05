using System;
using System.Security.Cryptography;

namespace Portunus.Core;

/// <summary>
/// A document a wire format wrote, in a pinned array that its disposal zeroes (<c>06</c>
/// PRT-ENG-005).
/// </summary>
internal sealed class PinnedDocument : IDisposable
{
    private byte[] _array;

    internal PinnedDocument(byte[] array, int length)
    {
        _array = array;
        Length = length;
    }

    /// <summary>The document's length in bytes.</summary>
    internal int Length { get; private set; }

    /// <summary>The document's bytes.</summary>
    internal ReadOnlySpan<byte> Span => _array.AsSpan(0, Length);

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_array);
        _array = [];
        Length = 0;
    }
}