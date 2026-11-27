using Sugarista.Application.MoqAlerts;
using Sugarista.Infrastructure.Notifications;

namespace Sugarista.Infrastructure.UnitTests;

public class MoqAlertBroadcasterTests
{
    [Fact]
    public async Task SubscribeAndNotify_PushesVersionImmediatelyAndCleansUpOnDispose()
    {
        var broadcaster = new MoqAlertBroadcaster();
        var (reader1, sub1) = broadcaster.Subscribe();
        var (reader2, sub2) = broadcaster.Subscribe();

        Assert.Equal(2, broadcaster.ActiveSubscriberCount);

        broadcaster.NotifyStockChanged();

        var v1 = await reader1.ReadAsync();
        var v2 = await reader2.ReadAsync();
        Assert.Equal(broadcaster.CurrentVersion, v1);
        Assert.Equal(broadcaster.CurrentVersion, v2);

        sub1.Dispose();
        Assert.Equal(1, broadcaster.ActiveSubscriberCount);

        sub2.Dispose();
        Assert.Equal(0, broadcaster.ActiveSubscriberCount);
    }

    [Fact]
    public async Task GetOrLoadSnapshotAsync_CachesPerVersionAndInvalidatesOnStockChange()
    {
        var broadcaster = new MoqAlertBroadcaster();
        var loadCalls = 0;

        Task<MoqAlertSnapshotDto> Loader(long version, CancellationToken _)
        {
            Interlocked.Increment(ref loadCalls);
            return Task.FromResult(new MoqAlertSnapshotDto(
                version,
                DateTimeOffset.UtcNow,
                1,
                0,
                1,
                0,
                5,
                []));
        }

        var first = await broadcaster.GetOrLoadSnapshotAsync(Loader);
        var second = await broadcaster.GetOrLoadSnapshotAsync(Loader);

        Assert.Equal(1, loadCalls);
        Assert.Same(first, second);

        // Stock changes -> cache invalidates immediately
        broadcaster.NotifyStockChanged();
        var third = await broadcaster.GetOrLoadSnapshotAsync(Loader);

        Assert.Equal(2, loadCalls);
        Assert.NotEqual(first.Version, third.Version);
        Assert.Equal(broadcaster.CurrentVersion, third.Version);
    }
}
