using System;
using System.Collections.Generic;
using System.Threading;

using Portunus.Core;

namespace Portunus.Agent;

/// <summary>
/// The agent's state and the set it holds (<c>01</c> PRT-AGT-001, PRT-AGT-005): waiting, with
/// a reason, or ready, with one set, and never back to waiting. A read leases the state it
/// answers from; a set that is replaced, and the set when the holder is disposed, are zeroed
/// once no lease still holds them.
/// </summary>
internal sealed class SetHolder(TimeProvider time) : IDisposable
{
    private readonly Lock _lock = new();

    private WaitReason _reason = WaitReason.Fetching;

    private DateTimeOffset _since = time.GetUtcNow();

    private Held? _held;

    /// <summary>Waits for <paramref name="reason"/> from now on; a ready holder stays ready.</summary>
    internal void Wait(WaitReason reason)
    {
        lock (_lock)
        {
            if (_held is null && reason != _reason)
            {
                _reason = reason;
                _since = time.GetUtcNow();
            }
        }
    }

    /// <summary>
    /// Takes <paramref name="set"/>, whose values <paramref name="values"/> owns, as the agent's
    /// set, at once and whole, where the holder waits or <paramref name="replace"/> is set, and
    /// answers whether it did; a set it replaces is zeroed once no read holds it. A set it does
    /// not take stays its caller's.
    /// </summary>
    internal bool TryFill(VaultSet set, PinnedValues values, SetSource source, bool replace)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(values);
        Held? replaced;
        lock (_lock)
        {
            if (_held is not null && !replace)
            {
                return false;
            }

            replaced = _held;
            DateTimeOffset now = time.GetUtcNow();
            _held = new Held(set, values, source, now);

            // A replaced set keeps the instant the agent became ready; the new one has its own load.
            if (replaced is null)
            {
                _since = now;
            }
        }

        replaced?.Retire();
        return true;
    }

    /// <summary>Whether the holder is ready.</summary>
    internal bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _held is not null;
            }
        }
    }

    /// <summary>Leases the state as it is now, for one answer.</summary>
    internal Lease Acquire()
    {
        lock (_lock)
        {
            _held?.Take();
            return new Lease(_held, _reason, _since);
        }
    }

    /// <summary>The arrays of the set held now, for a test's check that they are zeroed once replaced or stopped (<c>01</c> PRT-AGT-001 criterion 3).</summary>
    internal IReadOnlyList<byte[]> HeldArrays()
    {
        lock (_lock)
        {
            return _held is null ? [] : [.. _held.Values.Arrays];
        }
    }

    /// <summary>Zeroes the set once no read holds it.</summary>
    public void Dispose()
    {
        Held? held;
        lock (_lock)
        {
            held = _held;
            _held = null;
        }

        held?.Retire();
    }

    /// <summary>What one answer reads: the state, and the set while ready, held until the lease is disposed.</summary>
    internal readonly struct Lease : IDisposable
    {
        private readonly Held? _held;

        internal Lease(Held? held, WaitReason reason, DateTimeOffset since)
        {
            _held = held;
            Reason = reason;
            Since = since;
        }

        /// <summary>The state.</summary>
        internal AgentState State => _held is null ? AgentState.Waiting : AgentState.Ready;

        /// <summary>The reason, while waiting.</summary>
        internal WaitReason Reason { get; }

        /// <summary>The instant the state began.</summary>
        internal DateTimeOffset Since { get; }

        /// <summary>The set, while ready.</summary>
        internal VaultSet? Set => _held?.Set;

        /// <summary>Where the set came from, while ready.</summary>
        internal SetSource Source => _held?.Source ?? SetSource.Vault;

        /// <summary>The instant the set was loaded, while ready.</summary>
        internal DateTimeOffset LoadedAt => _held?.LoadedAt ?? Since;

        public void Dispose()
        {
            _held?.Release();
        }
    }

    /// <summary>A set, the owner of its values, and the reads that hold it.</summary>
    internal sealed class Held(VaultSet set, PinnedValues values, SetSource source, DateTimeOffset loadedAt)
    {
        private readonly Lock _lock = new();

        private int _leases;

        private bool _retired;

        internal VaultSet Set { get; } = set;

        internal PinnedValues Values { get; } = values;

        internal SetSource Source { get; } = source;

        internal DateTimeOffset LoadedAt { get; } = loadedAt;

        internal void Take()
        {
            lock (_lock)
            {
                _leases++;
            }
        }

        internal void Release()
        {
            lock (_lock)
            {
                _leases--;
                ZeroIfFree();
            }
        }

        internal void Retire()
        {
            lock (_lock)
            {
                _retired = true;
                ZeroIfFree();
            }
        }

        private void ZeroIfFree()
        {
            if (_retired && _leases == 0)
            {
                Values.Dispose();
            }
        }
    }
}