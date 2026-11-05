using System;
using System.IO;
using System.Security.Cryptography;

using Microsoft.Win32.SafeHandles;

namespace Portunus.Agent.Fetch;

/// <summary>
/// The service account's token (<c>01</c> PRT-AGT-003, <c>05</c> PRT-OPS-002): read from its
/// file into a pinned array at the start, as the file holds it, and zeroed when the agent no
/// longer needs it.
/// </summary>
internal static class TokenFile
{
    /// <summary>The token file's limit (<c>07</c> section 5).</summary>
    internal const int MostBytes = 4096;

    /// <summary>Reads the token at <paramref name="path"/>, or answers none where it cannot be read, is empty or is over 4 KiB.</summary>
    internal static byte[]? Read(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            long length = RandomAccess.GetLength(handle);
            if (length is 0 or > MostBytes)
            {
                return null;
            }

            byte[] token = GC.AllocateArray<byte>((int)length, pinned: true);
            int read = 0;
            while (read < token.Length)
            {
                int count = RandomAccess.Read(handle, token.AsSpan(read), read);
                if (count == 0)
                {
                    CryptographicOperations.ZeroMemory(token);
                    return null;
                }

                read += count;
            }

            return token;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}