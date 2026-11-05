using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Portunus.Core;

/// <summary>
/// The pinned arrays a reader of a wire format made for the values it read (<c>06</c>
/// PRT-ENG-005), each zeroed by the disposal of the whole.
/// </summary>
internal sealed class PinnedValues : IDisposable
{
    private readonly List<byte[]> _arrays = [];

    /// <summary>Makes a pinned array of <paramref name="length"/> bytes, which this owner zeroes.</summary>
    internal byte[] Allocate(int length)
    {
        byte[] array = GC.AllocateArray<byte>(length, pinned: true);
        _arrays.Add(array);
        return array;
    }

    /// <summary>The arrays this owner holds, for a test's check that they are zeroed (<c>01</c> PRT-AGT-001 criterion 3).</summary>
    internal IReadOnlyList<byte[]> Arrays => _arrays;

    public void Dispose()
    {
        foreach (byte[] array in _arrays)
        {
            CryptographicOperations.ZeroMemory(array);
        }

        _arrays.Clear();
    }
}