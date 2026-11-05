using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Portunus.Agent.Tests;

/// <summary>The agent's directories, sockets and peer check, in a running agent of the <c>push</c> mode, which waits (<c>01</c> PRT-AGT-005, PRT-AGT-006).</summary>
[Trait("Category", "linux")]
public sealed class AgentServingTests : IAsyncDisposable
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute;

    private const UnixFileMode SocketMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite;

    private readonly AgentFiles _files = AgentFiles.OfRoot();

    private readonly CapturedLog _log = new();

    private IHost? _host;

    private string Web => Path.Combine(_files.InstanceDirectory, "web");

    private string Push => Path.Combine(_files.InstanceDirectory, "push");

    [Fact]
    public async Task PRT_AGT_006_AC1_EachDirectoryAndSocketHasItsOwnerGroupAndMode()
    {
        await StartAsync();

        AssertOwned(Web, FileKind.Directory, 4242, DirectoryMode);
        AssertOwned(Path.Combine(Web, "agent.sock"), FileKind.Socket, 4242, SocketMode);
        AssertOwned(Push, FileKind.Directory, 4244, DirectoryMode);
        AssertOwned(Path.Combine(Push, "agent.sock"), FileKind.Socket, 4244, SocketMode);
    }

    [Fact]
    public async Task PRT_AGT_006_AC1_DirectoryFoundWithOtherOnesIsCorrected()
    {
        Directory.CreateDirectory(Web);
        File.SetUnixFileMode(Web, (UnixFileMode)0b111_111_111);
        Native.SetOwnership(Web, 4243, 0);
        ulong inode = Native.Ownership(Web)!.Inode;

        await StartAsync();

        AssertOwned(Web, FileKind.Directory, 4242, DirectoryMode);
        Assert.Equal(inode, Native.Ownership(Web)!.Inode);
    }

    [Fact]
    public async Task PRT_AGT_006_AC2_ConnectionFromAnotherUserIsClosedWithoutAnAnswerAndLogged()
    {
        await StartAsync();

        // The test runs as root, and the declaration names user 4243.
        string answer = await ExchangeAsync(Path.Combine(Web, "agent.sock"), """{"version":1,"kind":"status"}""");

        Assert.Empty(answer);
        Assert.Contains(_log.Lines, line => line == "connection to web's socket refused: peer user 0");
    }

    [Fact]
    public async Task PRT_AGT_006_AC3_RestartLeavesEachDirectoryInPlaceByInode()
    {
        await StartAsync();
        ulong web = Native.Ownership(Web)!.Inode;
        ulong push = Native.Ownership(Push)!.Inode;

        await StopAsync();
        Assert.False(File.Exists(Path.Combine(Web, "agent.sock")));
        await StartAsync();

        Assert.Equal(web, Native.Ownership(Web)!.Inode);
        Assert.Equal(push, Native.Ownership(Push)!.Inode);
        Assert.Contains("\"state\":\"waiting\"", await ExchangeAsync(Path.Combine(Push, "agent.sock"), """{"version":1,"kind":"status"}"""), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PRT_AGT_005_AC1_PushAgentAnswersWaitingForAPushOnThePushSocket()
    {
        await StartAsync();

        string answer = await ExchangeAsync(Path.Combine(Push, "agent.sock"), """{"version":1,"kind":"status"}""");

        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"awaitingpush","since":""", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServeAsync_ReadOnThePushSocket_AnswersRequestInvalid()
    {
        await StartAsync();

        string answer = await ExchangeAsync(Path.Combine(Push, "agent.sock"), """{"version":1,"kind":"read","address":"Themis/connection"}""");

        Assert.Equal("""{"version":1,"state":"waiting","failure":{"code":"secrets.request.invalid","details":{}}}""", answer);
    }

    [Fact]
    public async Task PRT_AGT_009_AC1_StoppedAgentRemovesItsSockets()
    {
        await StartAsync();

        await StopAsync();

        Assert.False(File.Exists(Path.Combine(Web, "agent.sock")));
        Assert.False(File.Exists(Path.Combine(Push, "agent.sock")));
        Assert.True(Directory.Exists(Web));
        Assert.Contains(_log.Lines, line => line == "agent sample-test stopped");
    }

    [Fact]
    public async Task PRT_AGT_005_AC2_StatusOfAWaitingAgentExitsThreeWithItsAnswer()
    {
        await StartAsync();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = await AgentCommand.MainAsync(["status", AgentFiles.Instance], _files.Paths, output, error);

        Assert.Equal(3, exit);
        Assert.StartsWith("""{"version":1,"state":"waiting","reason":"awaitingpush",""", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task PRT_AGT_005_AC2_StatusOfAnAgentItCannotReachExitsOne()
    {
        _files.WriteConfiguration();
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = await AgentCommand.MainAsync(["status", AgentFiles.Instance], _files.Paths, output, error);

        Assert.Equal(1, exit);
        Assert.Equal("""{"code":"secrets.agent.unreachable","details":{}}""", error.ToString().Trim());
        Assert.Empty(output.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _files.Dispose();
        _log.Dispose();
    }

    private static void AssertOwned(string path, FileKind kind, uint group, UnixFileMode mode)
    {
        FileOwnership owned = Native.Ownership(path)!;
        Assert.Equal(kind, owned.Kind);
        Assert.Equal(0u, owned.Owner);
        Assert.Equal(group, owned.Group);
        Assert.Equal(mode, owned.Mode);
    }

    /// <summary>Sends <paramref name="request"/> and answers what came back, empty where the agent closed the connection without an answer.</summary>
    private static async Task<string> ExchangeAsync(string socketPath, string request)
    {
        using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), TestContext.Current.CancellationToken);
        byte[] buffer = new byte[65536];
        int length = 0;
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(request), SocketFlags.None, TestContext.Current.CancellationToken);
            socket.Shutdown(SocketShutdown.Send);
            int count;
            while ((count = await socket.ReceiveAsync(buffer.AsMemory(length), SocketFlags.None, TestContext.Current.CancellationToken)) > 0)
            {
                length += count;
            }
        }
        catch (SocketException exception) when (exception.SocketErrorCode is SocketError.ConnectionReset or SocketError.Shutdown)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private async Task StartAsync()
    {
        _files.WriteConfiguration();
        AgentSettings settings = AgentConfiguration.Read(_files.File, AgentFiles.Instance).Value!;
        _host = AgentCommand.BuildHost(settings, new AgentToken(null), services => services.AddSingleton<ILoggerProvider>(_log));
        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    private async Task StopAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TestContext.Current.CancellationToken);
            _host.Dispose();
            _host = null;
        }
    }
}