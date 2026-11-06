using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Portunus.Tests;

/// <summary>
/// A fake agent on a real Unix domain socket (<c>06</c> PRT-ENG-008): it takes each
/// connection as the agent does, one request read until the client shuts down its sending
/// side, or until it holds one whole JSON document, and answers it with what
/// <see cref="Answer"/> gives for it, an answer recorded in <c>07</c> section 6's form, or
/// closes the connection without an answer where it gives none. The whole document ends the
/// request because Windows' Unix domain sockets at times never deliver the end of the client's
/// sending side to a read that waits for it; the client sends nothing after its request.
/// </summary>
internal sealed class FakeAgent : IAsyncDisposable
{
    private const int RequestBytes = 4096;

    private readonly Socket _listener;

    private readonly CancellationTokenSource _stopping = new();

    private readonly Task _serving;

    private readonly ConcurrentQueue<string> _requests = new();

    private FakeAgent(string path, Func<string, string?> answer)
    {
        Path = path;
        Answer = answer;
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(path));
        _listener.Listen();
        _serving = ServeAsync(_stopping.Token);
    }

    /// <summary>The socket's path.</summary>
    internal string Path { get; }

    /// <summary>What the agent answers a request with, or none to close without an answer.</summary>
    internal Func<string, string?> Answer { get; set; }

    /// <summary>Every request the agent read, in order.</summary>
    internal IReadOnlyList<string> Requests => [.. _requests];

    /// <summary>Starts an agent at <paramref name="path"/>, answering as <paramref name="answer"/> gives.</summary>
    internal static FakeAgent Start(string path, Func<string, string?> answer)
    {
        return new FakeAgent(path, answer);
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Dispose();
        try
        {
            await _serving;
        }
        catch (OperationCanceledException)
        {
            // The agent was stopped while it waited for a connection.
        }

        _stopping.Dispose();
        File.Delete(Path);
    }

    private async Task ServeAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            Socket connection;
            try
            {
                connection = await _listener.AcceptAsync(stopping);
            }
            catch (SocketException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (stopping.IsCancellationRequested)
            {
                return;
            }

            using (connection)
            {
                string request = await ReadRequestAsync(connection, stopping);
                _requests.Enqueue(request);
                string? answer = Answer(request);
                if (answer is not null)
                {
                    await connection.SendAsync(Encoding.UTF8.GetBytes(answer), SocketFlags.None, stopping);
                }

                connection.Shutdown(SocketShutdown.Both);
            }
        }
    }

    private static async Task<string> ReadRequestAsync(Socket connection, CancellationToken stopping)
    {
        byte[] request = new byte[RequestBytes];
        int length = 0;
        while (length < request.Length)
        {
            int count = await connection.ReceiveAsync(request.AsMemory(length), SocketFlags.None, stopping);
            if (count == 0)
            {
                break;
            }

            length += count;
            if (IsWhole(request.AsSpan(0, length)))
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(request, 0, length);
    }

    /// <summary>Whether <paramref name="read"/> holds one whole JSON document, the request's form (<c>07</c> section 6).</summary>
    private static bool IsWhole(ReadOnlySpan<byte> read)
    {
        try
        {
            Utf8JsonReader reader = new(read, isFinalBlock: false, state: default);
            return reader.Read() && reader.TrySkip();
        }
        catch (JsonException)
        {
            // Not a request's form: read on to the end of the sending side.
            return false;
        }
    }
}