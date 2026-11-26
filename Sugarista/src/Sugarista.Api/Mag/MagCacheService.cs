using Sugarista.Application.Blog;

namespace Sugarista.Api.Mag;

public sealed class MagCacheService : IMagCacheService
{
    private long _version = 1;

    public long Version => Interlocked.Read(ref _version);

    public void InvalidateAll() => Interlocked.Increment(ref _version);
}
