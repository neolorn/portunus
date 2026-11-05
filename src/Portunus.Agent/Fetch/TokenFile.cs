using System;
using System.IO;
using System.Security.Cryptography;

using Microsoft.Win32.SafeHandles;

namespace Portunus.Agent.Fetch;

/// <summary>
/// The service account's token (<c>01</c> PRT-AGT-003, <c>05</c> PRT-OPS-002): read from its
/// file into a pinned array at the start, as the file holds it less one final line ending
/// (P-031), and zeroed when the agent no longer needs it.
/// </summary>
internal static class TokenFile
{
    /// <summary>The token file's limit (<c>07</c> section 5).</summary>
    internal const int MostBytes = 4096;

    /// <summary>Reads the token at <paramref name="path"/> less one final <c>\n</c> or <c>\r\n</c>, or answers none where it cannot be read, is empty, holds a line ending alone or is over 4 KiB.</summary>
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

            byte[] file = GC.AllocateArray<byte>((int)length, pinned: true);
            try
            {
                int read = 0;
                while (read < file.Length)
                {
                    int count = RandomAccess.Read(handle, file.AsSpan(read), read);
                    if (count == 0)
                    {
                        return null;
                    }

                    read += count;
                }

                int kept = WithoutLineEnding(file);
                if (kept == 0)
                {
                    return null;
                }

                byte[] token = GC.AllocateArray<byte>(kept, pinned: true);
                file.AsSpan(0, kept).CopyTo(token);
                return token;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The length of <paramref name="file"/> less one final <c>\n</c> or <c>\r\n</c>; any other byte stays (P-031).</summary>
    private static int WithoutLineEnding(ReadOnlySpan<byte> file)
    {
        if (file is [.., (byte)'\r', (byte)'\n'])
        {
            return file.Length - 2;
        }

        return file is [.., (byte)'\n'] ? file.Length - 1 : file.Length;
    }
}