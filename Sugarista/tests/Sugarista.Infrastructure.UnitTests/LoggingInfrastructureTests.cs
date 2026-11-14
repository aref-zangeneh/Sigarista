using Serilog;
using Serilog.Core;
using Serilog.Events;
using Sugarista.Infrastructure.Logging;

namespace Sugarista.Infrastructure.UnitTests;

/// <summary>
/// Collects every emitted event (thread-safe) — the standard harness for
/// asserting what a decorator/enricher passed downstream.
/// </summary>
file sealed class CollectingSink : ILogEventSink
{
    private readonly object _gate = new();

    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            Events.Add(logEvent);
        }
    }
}

public sealed class RepeatSuppressingSinkTests
{
    [Fact]
    public void IdenticalConsecutiveEventsInsideWindow_AreCollapsed()
    {
        var collector = new CollectingSink();
        var sink = new RepeatSuppressingSink(collector, TimeSpan.FromSeconds(5));
        var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Health check 200");
        logger.Information("Health check 200");
        logger.Information("Health check 200");
        // A different event closes the series.
        logger.Information("Something else");

        var repeats = collector.Events
            .Where(e => e.MessageTemplate.Text.Contains("Repeated log suppressed"))
            .ToList();

        // first real event + 1 aggregate + the new real event
        Assert.Equal(3, collector.Events.Count);
        Assert.Single(repeats);
        // The aggregate states the total occurrences of the collapsed series.
        Assert.Equal(3, ((ScalarValue)repeats[0].Properties["RepeatCount"]).Value);
    }

    [Fact]
    public void SameTemplateDifferentContent_IsNotSuppressed()
    {
        var collector = new CollectingSink();
        var sink = new RepeatSuppressingSink(collector, TimeSpan.FromSeconds(5));
        var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Order 1 saved");
        logger.Information("Order 2 saved");

        Assert.Equal(2, collector.Events.Count);
    }
}

public sealed class SensitiveDataEnricherTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("AdminPassword")]
    [InlineData("accessToken")]
    [InlineData("Authorization")]
    [InlineData("ConnectionString")]
    [InlineData("ApiKey")]
    public void SensitivePropertyNames_AreRedacted(string propertyName)
    {
        var collector = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataEnricher())
            .WriteTo.Sink(collector)
            .CreateLogger();

        // The template uses the actual InlineData property name so the
        // enricher must catch every sensitive NAME variant.
        logger.Information("Login attempt with {" + propertyName + "}", "raw-secret");

        var emitted = collector.Events.Single();
        var scalar = Assert.IsType<ScalarValue>(emitted.Properties[propertyName]);
        Assert.Equal("«redacted»", scalar.Value);
    }
}

public sealed class BoundedDroppingSinkTests
{
    /// <summary>Blocks the drain thread on the first event until released —
    /// that is what fills the bounded queue in the overflow test.</summary>
    private sealed class BlockingSink(Action release) : ILogEventSink, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new(false);

        public int Received;

        public void Emit(LogEvent logEvent)
        {
            _gate.Wait(TimeSpan.FromSeconds(10));
            Interlocked.Increment(ref Received);
        }

        public void Release() => _gate.Set();

        public void Dispose() => release();
    }

    [Fact]
    public void Overflow_DropsAndCounts_NeverBlocks()
    {
        var blockingSink = new BlockingSink(() => { });
        using var sink = new BoundedDroppingSink(blockingSink, capacity: 100);
        var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // The drain thread is stuck on the first event, so the bounded queue
        // fills up — everything after capacity is dropped WITHOUT blocking
        // this thread. (The sink enforces a minimum internal capacity of 64.)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var index = 0; index < 200; index++)
        {
            logger.Information("Event {Number}", index);
        }
        stopwatch.Stop();

        sink.Flush(TimeSpan.FromSeconds(1)); // queue is stuck by design here

        // Drain may have consumed 0 or 1 event before blocking, so the split
        // between accepted and dropped has ±1 slack — but the total is exact.
        Assert.Equal(200, sink.AcceptedCount + sink.DroppedCount);
        Assert.InRange(sink.DroppedCount, 99, 100);
        Assert.Equal(0, blockingSink.Received); // drain still blocked
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "Emit must never block on a full queue");

        blockingSink.Release();
    }

    [Fact]
    public void Flush_DeliversQueuedEvents()
    {
        var collector = new CollectingSink();
        using var sink = new BoundedDroppingSink(collector, capacity: 100);
        var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("One");
        logger.Information("Two");
        sink.Flush(TimeSpan.FromSeconds(5));

        Assert.Equal(2, collector.Events.Count);
        Assert.Equal(0, sink.DroppedCount);
    }
}
