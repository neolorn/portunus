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
/// peer checked by user identifier on each connection, and the push socket, which takes a push
/// (<c>02</c> PRT-PSH-003). A connection carries one request, of at most 4 KiB on a process's
/// socket and 2 MiB on the push socket, received within 5 seconds, and one answer; at most 64
/// are served at once.
/// </summary>
internal sealed class AgentServer(AgentSettings settings, SetHolder holder, AgentToken token, TimeProvider time, ILogger log) : IDisposable
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
            uint? peer = PeerUser(connection);
            string peerName = peer?.ToString(CultureInfo.InvariantCulture) ?? UnknownPeer;
            if (declaration is not null && peer != declaration.Uid)
            {
                // A peer whose credentials cannot be read is refused as another user is (P-032).
                AgentLog.PeerRefused(log, declaration.Process, peerName);
                return;
            }

            using PinnedDocument? request = await ReceiveAsync(connection, declaration is null ? Limits.AnswerBytes : Limits.RequestBytes).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            using PinnedDocument answer = declaration is null ? AnswerOnPushSocket(request.Span, peerName) : Answer(request.Span, declaration);
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

    /// <summary>The request, read until the peer shuts down its sending side; or none where it passes <paramref name="most"/> bytes or 5 seconds.</summary>
    private async Task<PinnedDocument?> ReceiveAsync(Socket connection, int most)
    {
        using CancellationTokenSource bound = new(RequestTime, time);
        using PinnedBufferWriter request = new(most);
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

    /// <summary>Answers a request on a process's socket: a status or a read; a push there is never taken.</summary>
    private PinnedDocument Answer(ReadOnlySpan<byte> document, Declaration declaration)
    {
        Result<WireRequest> request = RequestFormat.Read(document, Limits.RequestBytes);
        using SetHolder.Lease lease = holder.Acquire();
        switch (request.Value)
        {
            case null:
                return AnswerFormat.WriteFailure(lease.State, request.Failure!);
            case WireRequest.Status:
                return Status(lease);
            case WireRequest.Read read:
                return Read(read.Address, declaration, lease);
            case WireRequest.Push push:
                push.Values.Dispose();
                return AnswerFormat.WriteFailure(lease.State, WireJson.Invalid());
            default:
                return AnswerFormat.WriteFailure(lease.State, WireJson.Invalid());
        }
    }

    /// <summary>Answers a request on the push socket: a status or a push; a read there answers <c>secrets.request.invalid</c>.</summary>
    private PinnedDocument AnswerOnPushSocket(ReadOnlySpan<byte> document, string peer)
    {
        Result<WireRequest> request = RequestFormat.Read(document, Limits.AnswerBytes);
        if (request.Value is WireRequest.Push push)
        {
            return Take(push, peer);
        }

        using SetHolder.Lease lease = holder.Acquire();
        return request.Value switch
        {
            null => AnswerFormat.WriteFailure(lease.State, request.Failure!),
            WireRequest.Status => Status(lease),
            _ => AnswerFormat.WriteFailure(lease.State, WireJson.Invalid()),
        };
    }

    private static PinnedDocument Status(SetHolder.Lease lease)
    {
        return lease.Set is VaultSet held
            ? AnswerFormat.WriteReady(lease.Since, lease.Source, lease.LoadedAt, held.Items.Count)
            : AnswerFormat.WriteWaiting(lease.Reason, lease.Since);
    }

    /// <summary>
    /// Takes a push (<c>02</c> PRT-PSH-003) only for the agent's own vault, its title compared
    /// as <c>03</c> PRT-VLT-001 compares titles, only where its set passes the layout, and only
    /// into a waiting agent unless it says to replace; once taken, the fetch's attempts end, since
    /// the holder is ready, and the token is dropped. Each push is logged with the peer, the
    /// outcome and the number of items, never a value.
    /// </summary>
    private PinnedDocument Take(WireRequest.Push push, string peer)
    {
        PinnedValues? values = push.Values;
        try
        {
            int items = push.Set.Items.Count;
            Failure? refusal = null;
            if (!string.Equals(push.Set.Vault, settings.Options.Vault, StringComparison.OrdinalIgnoreCase))
            {
                refusal = new Failure { Code = Codes.PushWrongVault };
            }
            else if (LayoutValidator.FirstError(LayoutValidator.Check(push.Set)) is LayoutFinding error)
            {
                refusal = new Failure { Code = Codes.PushLayoutInvalid, Details = new Dictionary<string, string> { ["item"] = error.Item ?? string.Empty, ["field"] = error.Field ?? string.Empty } };
            }
            else if (holder.TryFill(push.Set, push.Values, SetSource.Push, push.Replace))
            {
                values = null;
            }
            else
            {
                refusal = new Failure { Code = Codes.PushReady };
            }

            if (refusal is not null)
            {
                AgentLog.Push(log, peer, refusal.Code, items);
                return PushAnswerFormat.WriteRefused(refusal);
            }

            token.Drop();
            AgentLog.Push(log, peer, "accepted", items);
            AgentLog.Ready(log, items, "push");
            return PushAnswerFormat.WriteAccepted(items);
        }
        finally
        {
            values?.Dispose();
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