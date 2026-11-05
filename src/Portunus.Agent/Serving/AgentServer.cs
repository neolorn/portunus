using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Portunus.Core;

namespace Portunus.Agent.Serving;

/// <summary>
/// The agent's sockets (<c>01</c> PRT-AGT-006 to PRT-AGT-008): one per declared process, its
/// peer checked by user identifier on each connection, and the push socket. A connection
/// carries one request of at most 4 KiB, received within 5 seconds, and one answer; at most 64
/// are served at once.
/// </summary>
internal sealed class AgentServer(AgentSettings settings, SetHolder holder, TimeProvider time, ILogger log) : IDisposable
{
    private const int MostConnections = 64;

    private const int SolSocket = 1;

    private const int PeerCredentials = 17;

    private const string UnknownPeer = "unknown";

    private static readonly TimeSpan RequestTime = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _slots = new(MostConnections, MostConnections);

    private readonly ConcurrentDictionary<long, Task> _serving = new();

    private readonly ConcurrentDictionary<long, Socket> _open = new();

    private readonly CancellationTokenSource _stopping = new();

    private readonly List<(Socket Listener, string Directory, Declaration? Declaration)> _endpoints = [];

    private readonly List<Task> _accepting = [];

    private long _connections;

    /// <summary>Prepares each directory and binds each socket, then accepts connections until stopped.</summary>
    internal void Start()
    {
        uint agent = SocketDirectories.AgentUser();
        SocketDirectories.PrepareInstance(settings.InstanceDirectory);
        foreach (Declaration declaration in settings.Declarations)
        {
            string directory = declaration.Directory(settings.InstanceDirectory);
            _endpoints.Add((SocketDirectories.Bind(directory, agent, declaration.Gid), directory, declaration));
        }

        _endpoints.Add((SocketDirectories.Bind(settings.PushDirectory, agent, settings.Options.PushGroup), settings.PushDirectory, null));
        CancellationToken stopping = _stopping.Token;
        for (int endpoint = 0; endpoint < _endpoints.Count; endpoint++)
        {
            _accepting.Add(AcceptAsync(endpoint, stopping));
        }
    }

    /// <summary>Stops accepting, lets the reads under way finish within 5 seconds, and removes the sockets.</summary>
    internal async Task StopAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        foreach ((Socket listener, string _, Declaration? _) in _endpoints)
        {
            listener.Dispose();
        }

        await Task.WhenAll(_accepting).ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_serving.Values).WaitAsync(StopGrace, time).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A read that has not finished within the stop's grace is left; its lease keeps the set until it ends.
        }

        foreach ((Socket _, string directory, Declaration? _) in _endpoints)
        {
            SocketDirectories.Unbind(directory);
        }
    }

    public void Dispose()
    {
        _stopping.Dispose();
        _slots.Dispose();
    }

    private async Task AcceptAsync(int endpoint, CancellationToken stopping)
    {
        (Socket listener, string _, Declaration? declaration) = _endpoints[endpoint];
        while (!stopping.IsCancellationRequested)
        {
            Socket connection;
            try
            {
                connection = await listener.AcceptAsync(stopping).ConfigureAwait(false);
            }
            catch (Exception exception) when (stopping.IsCancellationRequested && exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            if (!_slots.Wait(0, CancellationToken.None))
            {
                // A connection beyond the 64 served at once is closed without an answer.
                connection.Dispose();
                continue;
            }

            long id = Interlocked.Increment(ref _connections);
            _open[id] = connection;
            _serving[id] = ServeAsync(id, declaration);
        }
    }

    /// <summary>Serves the connection <paramref name="id"/> names, which it owns and disposes.</summary>
    private async Task ServeAsync(long id, Declaration? declaration)
    {
        await Task.Yield();
        _open.TryRemove(id, out Socket? taken);
        using Socket connection = taken!;
        try
        {
            if (declaration is not null)
            {
                // A peer whose credentials cannot be read is refused as another user is (P-032).
                uint? peer = PeerUser(connection);
                if (peer != declaration.Uid)
                {
                    AgentLog.PeerRefused(log, declaration.Process, peer?.ToString(CultureInfo.InvariantCulture) ?? UnknownPeer);
                    return;
                }
            }

            using PinnedDocument? request = await ReceiveAsync(connection).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            using PinnedDocument answer = Answer(request.Span, declaration);
            for (int sent = 0; sent < answer.Length;)
            {
                sent += await connection.SendAsync(answer.Memory[sent..], SocketFlags.None).ConfigureAwait(false);
            }

            connection.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // The peer went away; nothing is answered.
        }
        finally
        {
            _slots.Release();
            _serving.TryRemove(id, out Task? _);
        }
    }

    /// <summary>The peer's user identifier, from <c>SO_PEERCRED</c>; none where it cannot be read.</summary>
    private static uint? PeerUser(Socket connection)
    {
        Span<byte> credentials = stackalloc byte[12];
        try
        {
            return connection.GetRawSocketOption(SolSocket, PeerCredentials, credentials) == 12 ? BitConverter.ToUInt32(credentials[4..]) : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>The request, read until the peer shuts down its sending side; or none where it passes 4 KiB or 5 seconds.</summary>
    private async Task<PinnedDocument?> ReceiveAsync(Socket connection)
    {
        using CancellationTokenSource bound = new(RequestTime, time);
        using PinnedBufferWriter request = new(Limits.RequestBytes);
        try
        {
            while (true)
            {
                int count = await connection.ReceiveAsync(request.GetMemory(Limits.RequestBytes), SocketFlags.None, bound.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    return request.Detach();
                }

                request.Advance(count);
                if (request.IsOverBound)
                {
                    return null;
                }
            }
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested)
        {
            return null;
        }
    }

    private PinnedDocument Answer(ReadOnlySpan<byte> document, Declaration? declaration)
    {
        Result<WireRequest> request = RequestFormat.Read(document, Limits.RequestBytes);
        using SetHolder.Lease lease = holder.Acquire();
        switch (request.Value)
        {
            case null:
                return AnswerFormat.WriteFailure(lease.State, request.Failure!);
            case WireRequest.Status:
                return lease.Set is VaultSet held
                    ? AnswerFormat.WriteReady(lease.Since, lease.Source, lease.Since, held.Items.Count)
                    : AnswerFormat.WriteWaiting(lease.Reason, lease.Since);
            case WireRequest.Read read when declaration is not null:
                return Read(read.Address, declaration, lease);
            case WireRequest.Push push:
                // The push's taking is 02 PRT-PSH-003's; a push on a process's socket is never taken.
                push.Values.Dispose();
                return AnswerFormat.WriteFailure(lease.State, WireJson.Invalid());
            default:
                // A read on the push socket.
                return AnswerFormat.WriteFailure(lease.State, WireJson.Invalid());
        }
    }

    private PinnedDocument Read(SecretAddress address, Declaration declaration, SetHolder.Lease lease)
    {
        if (!declaration.Secrets.Contains(address))
        {
            AgentLog.Read(log, declaration.Process, address, Codes.SecretDenied);
            return AnswerFormat.WriteFailure(lease.State, new Failure { Code = Codes.SecretDenied });
        }

        if (lease.Set is not VaultSet set)
        {
            AgentLog.Read(log, declaration.Process, address, "waiting");
            return AnswerFormat.WriteWaiting(lease.Reason, lease.Since);
        }

        if (set.Find(address) is not Secret secret)
        {
            AgentLog.Read(log, declaration.Process, address, Codes.SecretUnknown);
            return AnswerFormat.WriteFailure(AgentState.Ready, new Failure { Code = Codes.SecretUnknown });
        }

        AgentLog.Read(log, declaration.Process, address, "answered");
        return AnswerFormat.WriteSecret(secret.Current, secret.Versions);
    }
}