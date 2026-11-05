using System;
using System.IO;
using System.Net.Sockets;

namespace Portunus.Agent.Serving;

/// <summary>
/// The instance's directories and sockets (<c>01</c> PRT-AGT-006): each declared process's
/// directory, its group the process's, mode <c>0710</c>, and the push directory, its group
/// <c>Portunus:Agent:PushGroup</c>, each owned by the agent's user and holding
/// <c>agent.sock</c>, its group the directory's, mode <c>0660</c>. A missing directory is made,
/// one found with another owner, group or mode is corrected, and none is ever deleted; a stale
/// socket is removed before a new one is bound.
/// </summary>
internal static class SocketDirectories
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute;

    private const UnixFileMode SocketMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite;

    // The instance's own directory, as the unit's RuntimeDirectoryMode makes it (07 section 7).
    private const UnixFileMode InstanceMode = DirectoryMode | UnixFileMode.OtherExecute;

    /// <summary>The agent's own user identifier, the owner of what the process makes (<c>/proc/self</c> is the process's).</summary>
    internal static uint AgentUser()
    {
        return Native.Ownership("/proc/self")?.Owner ?? throw new IOException("The process's own entry in /proc is missing.");
    }

    /// <summary>Makes <paramref name="directory"/>, the instance's directory, where it is missing.</summary>
    internal static void PrepareInstance(string directory)
    {
        if (Native.Ownership(directory) is null)
        {
            Directory.CreateDirectory(directory, InstanceMode);
        }
    }

    /// <summary>Makes or corrects <paramref name="directory"/>, owned by <paramref name="owner"/> and of group <paramref name="group"/>, and binds its socket.</summary>
    /// <exception cref="IOException">The path is not a directory, its socket's path is held by another kind of file, or a call failed.</exception>
    internal static Socket Bind(string directory, uint owner, uint group)
    {
        FileOwnership? found = Native.Ownership(directory);
        if (found is null)
        {
            Directory.CreateDirectory(directory, DirectoryMode);
        }
        else if (found.Kind != FileKind.Directory)
        {
            throw new IOException($"{directory} is not a directory.");
        }

        Native.SetOwnership(directory, owner, group);
        File.SetUnixFileMode(directory, DirectoryMode);

        string path = Path.Combine(directory, AgentNames.Socket);
        FileOwnership? stale = Native.Ownership(path);
        if (stale is not null)
        {
            if (stale.Kind != FileKind.Socket)
            {
                throw new IOException($"{path} is not a socket.");
            }

            File.Delete(path);
        }

        Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(path));
            Native.SetOwnership(path, owner, group);
            File.SetUnixFileMode(path, SocketMode);
            socket.Listen();
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Removes the socket of <paramref name="directory"/>, leaving the directory in place.</summary>
    internal static void Unbind(string directory)
    {
        string path = Path.Combine(directory, AgentNames.Socket);
        if (Native.Ownership(path) is { Kind: FileKind.Socket })
        {
            File.Delete(path);
        }
    }
}