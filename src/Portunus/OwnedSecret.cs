using System;
using System.Collections.Generic;

using Portunus.Core;

namespace Portunus;

/// <summary>
/// The secret a read answers, in new pinned arrays of the caller's own (<c>04</c>
/// PRT-LIB-004, <c>06</c> PRT-ENG-005), so the buffers it was copied from stay the client's
/// to zero.
/// </summary>
internal static class OwnedSecret
{
    /// <summary>Copies each version of <paramref name="versions"/> into a pinned array of its own.</summary>
    internal static Secret Copy(int current, IReadOnlyDictionary<int, ReadOnlyMemory<byte>> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        Dictionary<int, ReadOnlyMemory<byte>> owned = [];
        foreach (KeyValuePair<int, ReadOnlyMemory<byte>> version in versions)
        {
            byte[] array = GC.AllocateArray<byte>(version.Value.Length, pinned: true);
            version.Value.Span.CopyTo(array);
            owned[version.Key] = array;
        }

        return new Secret { Current = current, Versions = owned };
    }
}