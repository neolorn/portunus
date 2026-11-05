using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace Portunus.Agent;

/// <summary>
/// The agent's three calls into the C library (<c>06</c> PRT-ENG-004, P-026), each through a
/// source-generated <c>LibraryImport</c>: <c>prctl</c>, to mark the agent not dumpable
/// (<c>01</c> PRT-AGT-001); <c>statx</c>, to read an owner, a group and a mode; and
/// <c>fchownat</c> with <c>AT_SYMLINK_NOFOLLOW</c>, to set an owner and a group (<c>01</c>
/// PRT-AGT-006, <c>05</c> PRT-OPS-001). Neither of the last two follows a link.
/// </summary>
internal static partial class Native
{
    private const string Library = "libc";

    private const int GetDumpable = 3;

    private const int SetDumpable = 4;

    private const int CurrentDirectory = -100;

    private const int SymlinkNoFollow = 0x100;

    // STATX_TYPE, STATX_MODE, STATX_UID, STATX_GID and STATX_INO.
    private const uint OwnerAndMode = 0x1 | 0x2 | 0x8 | 0x10 | 0x100;

    private const int StatxBytes = 256;

    private const ushort TypeMask = 0xF000;

    private const ushort DirectoryType = 0x4000;

    private const ushort SocketType = 0xC000;

    private const ushort RegularType = 0x8000;

    /// <summary>Marks the process not dumpable, so no core dump of it is written and no process of its own user but root reads its memory.</summary>
    /// <exception cref="IOException">The call failed.</exception>
    internal static void MarkNotDumpable()
    {
        if (Prctl(SetDumpable, 0, 0, 0, 0) != 0)
        {
            throw new IOException($"prctl(PR_SET_DUMPABLE) failed with errno {Marshal.GetLastPInvokeError()}.");
        }
    }

    /// <summary>Whether the process is dumpable, as <c>prctl(PR_GET_DUMPABLE)</c> answers it.</summary>
    internal static int Dumpable()
    {
        return Prctl(GetDumpable, 0, 0, 0, 0);
    }

    /// <summary>The owner, group, kind and permission bits of <paramref name="path"/>, a link read as itself; or none where it does not exist.</summary>
    /// <exception cref="IOException">The call failed for another reason than a missing path.</exception>
    internal static unsafe FileOwnership? Ownership(string path)
    {
        byte* buffer = stackalloc byte[StatxBytes];
        if (Statx(CurrentDirectory, path, SymlinkNoFollow, OwnerAndMode, buffer) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            return error == 2 ? null : throw new IOException($"statx failed with errno {error}.");
        }

        ReadOnlySpan<byte> result = new(buffer, StatxBytes);
        ushort mode = BinaryPrimitives.ReadUInt16LittleEndian(result[28..]);
        FileKind kind = (mode & TypeMask) switch
        {
            DirectoryType => FileKind.Directory,
            SocketType => FileKind.Socket,
            RegularType => FileKind.File,
            _ => FileKind.Other,
        };
        return new FileOwnership(BinaryPrimitives.ReadUInt32LittleEndian(result[20..]), BinaryPrimitives.ReadUInt32LittleEndian(result[24..]), kind, (UnixFileMode)(mode & 0xFFF), BinaryPrimitives.ReadUInt64LittleEndian(result[32..]));
    }

    /// <summary>Sets the owner and group of <paramref name="path"/>, a link set as itself and never followed.</summary>
    /// <exception cref="IOException">The call failed.</exception>
    internal static void SetOwnership(string path, uint owner, uint group)
    {
        if (Fchownat(CurrentDirectory, path, owner, group, SymlinkNoFollow) != 0)
        {
            throw new IOException($"fchownat failed with errno {Marshal.GetLastPInvokeError()}.");
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(Library, EntryPoint = "prctl", SetLastError = true)]
    private static partial int Prctl(int option, nuint second, nuint third, nuint fourth, nuint fifth);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(Library, EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int Statx(int directory, string path, int flags, uint mask, byte* buffer);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(Library, EntryPoint = "fchownat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Fchownat(int directory, string path, uint owner, uint group, int flags);
}