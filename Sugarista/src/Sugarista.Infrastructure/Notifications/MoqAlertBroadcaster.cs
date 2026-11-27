using System.Collections.Concurrent;
using System.Threading.Channels;
using Sugarista.Application.MoqAlerts;

namespace Sugarista.Infrastructure.Notifications;

/// <summary>
/// High-performance, lock-free in-memory broadcaster for real-time MOQ alerts.
/// Uses bounded channels per connected SSE client and version-keyed snapshot
/// caching so multiple connected admins share a single fast DB read per change.
/// </summary>
public sealed class MoqAlertBroadcaster : IMoqAlertBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<long>> _subscribers = new();
    private readonly SemaphoreSlim _snapshotLock = new(1, 1);

    private long _version = 1;
    private MoqAlertSnapshotDto? _cachedSnapshot;
    private long _cachedVersion = -1;

    public long CurrentVersion => Volatile.Read(ref _version);

    public int ActiveSubscriberCount => _subscribers.Count;

    public void NotifyStockChanged()
    {
        var nextVersion = Interlocked.Increment(ref _version);
        Volatile.Write(ref _cachedVersion, -1);

        foreach (var pair in _subscribers)
        {
            pair.Value.Writer.TryWrite(nextVersion);
        }
    }

    public (ChannelReader<long> Reader, IDisposable Subscription) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<long>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers[id] = channel;
        return (channel.Reader, new SubscriptionHandle(_subscribers, id, channel));
    }

    public async Task<MoqAlertSnapshotDto> GetOrLoadSnapshotAsync(
        Func<long, CancellationToken, Task<MoqAlertSnapshotDto>> loader,
        CancellationToken cancellationToken = default)
    {
        var targetVersion = CurrentVersion;
        var snapshot = _cachedSnapshot;
        if (snapshot is not null && Volatile.Read(ref _cachedVersion) == targetVersion)
        {
            return snapshot;
        }

        await _snapshotLock.WaitAsync(cancellationToken);
        try
        {
            targetVersion = CurrentVersion;
            if (_cachedSnapshot is not null && Volatile.Read(ref _cachedVersion) == targetVersion)
            {
                return _cachedSnapshot;
            }

            var loaded = await loader(targetVersion, cancellationToken);

            // Only cache if no newer stock mutation arrived while the query was running.
            if (CurrentVersion == targetVersion)
            {
                _cachedSnapshot = loaded;
                Volatile.Write(ref _cachedVersion, targetVersion);
            }

            return loaded;
        }
        finally
        {
            _snapshotLock.Release();
        }
    }

    private sealed class SubscriptionHandle : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, Channel<long>> _subscribers;
        private readonly Guid _id;
        private readonly Channel<long> _channel;
        private int _disposed;

        public SubscriptionHandle(
            ConcurrentDictionary<Guid, Channel<long>> subscribers,
            Guid id,
            Channel<long> channel)
        {
            _subscribers = subscribers;
            _id = id;
            _channel = channel;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _subscribers.TryRemove(_id, out _);
            _channel.Writer.TryComplete();
        }
    }
}
