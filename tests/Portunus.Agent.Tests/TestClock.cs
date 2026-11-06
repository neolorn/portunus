using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Portunus.Agent.Tests;

/// <summary>
/// A clock a test moves by hand (<c>06</c> PRT-ENG-008): its time stands still until the test
/// advances it, and a timer fires only when the time it is due at is reached.
/// </summary>
internal sealed class TestClock : TimeProvider
{
    private static readonly TimeSpan PatienceForATimer = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();

    private readonly List<ClockTimer> _timers = [];

    private TimeSpan _elapsed;

    /// <summary>The instant the clock starts at.</summary>
    internal static DateTimeOffset Start { get; } = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The time the test has advanced the clock by.</summary>
    internal TimeSpan Elapsed
    {
        get
        {
            lock (_lock)
            {
                return _elapsed;
            }
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        return Start + Elapsed;
    }

    public override long GetTimestamp()
    {
        return Elapsed.Ticks;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ClockTimer timer = new(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Waits, in real time, for a timer due within <paramref name="most"/> of now, and answers how long from now it is due.</summary>
    internal async Task<TimeSpan> NextTimerAsync(TimeSpan most)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (waited.Elapsed < PatienceForATimer)
        {
            lock (_lock)
            {
                TimeSpan? due = _timers.Where(timer => timer.DueAt - _elapsed <= most).Select(timer => (TimeSpan?)(timer.DueAt - _elapsed)).Min();
                if (due is TimeSpan found)
                {
                    return found;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        throw new TimeoutException("No timer was made within the test's patience.");
    }

    /// <summary>Advances the clock by <paramref name="time"/>, firing each timer due on the way, in order.</summary>
    internal void Advance(TimeSpan time)
    {
        TimeSpan target;
        lock (_lock)
        {
            target = _elapsed + time;
        }

        while (true)
        {
            ClockTimer? due;
            lock (_lock)
            {
                due = _timers.Where(timer => timer.DueAt <= target).OrderBy(timer => timer.DueAt).FirstOrDefault();
                if (due is null)
                {
                    _elapsed = target;
                    return;
                }

                _elapsed = due.DueAt;
                _timers.Remove(due);
                if (due.Period != Timeout.InfiniteTimeSpan && due.Period > TimeSpan.Zero)
                {
                    due.DueAt += due.Period;
                    _timers.Add(due);
                }
            }

            due.Fire();
        }
    }

    private void Schedule(ClockTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                return;
            }

            timer.DueAt = _elapsed + dueTime;
            timer.Period = period;
            _timers.Add(timer);
        }
    }

    private void Cancel(ClockTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ClockTimer(TestClock clock, TimerCallback callback, object? state) : ITimer
    {
        internal TimeSpan DueAt { get; set; }

        internal TimeSpan Period { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.Schedule(this, dueTime, period);
            return true;
        }

        public void Dispose()
        {
            clock.Cancel(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void Fire()
        {
            callback(state);
        }
    }
}