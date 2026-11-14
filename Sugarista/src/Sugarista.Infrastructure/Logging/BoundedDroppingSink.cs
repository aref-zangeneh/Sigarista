using System.Collections.Concurrent;
using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace Sugarista.Infrastructure.Logging;

/// <summary>
/// Bounded asynchronous file sink. Request threads only hand the event to an
/// in-memory queue of a FIXED size (no unbounded buffering, no RAM blow-up);
/// a dedicated background thread drains it to the real file sink.
///
/// Overflow behaviour (defined, never blocking): when the queue is full the
/// event is dropped and counted — user requests are never slowed down, and
/// the drop counter is exposed through the admin logging diagnostics.
///
/// Flush(): called on normal shutdown so buffered events reach the file
/// before the process exits.
/// </summary>
public sealed class BoundedDroppingSink : ILogEventSink, IDisposable
{
    private readonly BlockingCollection<LogEvent> _queue;
    private readonly Thread _drainThread;
    private readonly ILogEventSink _inner;
    private long _dropped;
    private long _accepted;
    private volatile bool _stopped;

    public BoundedDroppingSink(ILogEventSink inner, int capacity)
    {
        _inner = inner;
        _queue = new BlockingCollection<LogEvent>(Math.Max(64, capacity));
        _drainThread = new Thread(DrainLoop)
        {
            Name = "sugarista-log-drain",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal // logging must never fight request threads
        };
        _drainThread.Start();
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public long AcceptedCount => Interlocked.Read(ref _accepted);

    public int QueueDepth => _stopped ? 0 : _queue.Count;

    public int Capacity => _queue.BoundedCapacity;

    public void Emit(LogEvent logEvent)
    {
        if (_stopped)
        {
            // Shutdown already in progress: drop silently (also keeps the
            // drain thread from being restarted indirectly).
            Interlocked.Increment(ref _dropped);
            return;
        }

        // TryAdd on a bounded collection: never allocates beyond the cap,
        // never blocks the caller.
        if (_queue.TryAdd(logEvent))
        {
            Interlocked.Increment(ref _accepted);
        }
        else
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>Waits (bounded) until the queue is drained into the inner sink.</summary>
    public void Flush(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (QueueDepth > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }
    }

    private void DrainLoop()
    {
        try
        {
            foreach (var logEvent in _queue.GetConsumingEnumerable())
            {
                try
                {
                    _inner.Emit(logEvent);
                }
                catch
                {
                    // A failing file sink (disk error, permissions, …) must
                    // never crash the drain thread nor the application.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // CompleteAdding() — normal shutdown path.
        }
    }

    public void Dispose()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _queue.CompleteAdding();
        _drainThread.Join(TimeSpan.FromSeconds(5));
        (_inner as IDisposable)?.Dispose();
        _queue.Dispose();
    }
}
