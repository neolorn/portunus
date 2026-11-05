using System.IO;

namespace Portunus.Agent;

/// <summary>What <c>statx</c> answers of a path (P-026): its owner, its group, its kind, its permission bits and its inode.</summary>
/// <param name="Owner">The owner's user identifier.</param>
/// <param name="Group">The group's identifier.</param>
/// <param name="Kind">What the path is.</param>
/// <param name="Mode">The permission bits.</param>
/// <param name="Inode">The inode, which tells a directory kept from one made again.</param>
internal sealed record FileOwnership(uint Owner, uint Group, FileKind Kind, UnixFileMode Mode, ulong Inode);

/// <summary>What a path is, of the kinds the agent tells apart.</summary>
internal enum FileKind
{
    /// <summary>A regular file.</summary>
    File,

    /// <summary>A directory.</summary>
    Directory,

    /// <summary>A Unix domain socket.</summary>
    Socket,

    /// <summary>Anything else, a link included.</summary>
    Other,
}