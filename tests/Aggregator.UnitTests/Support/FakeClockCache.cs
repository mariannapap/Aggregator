using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Support;

/// <summary>A HybridCache whose expiry follows a fake clock: its in-memory layer reads time from MemoryCacheOptions.Clock, not from TimeProvider.</summary>
internal static class FakeClockCache
{
    public static ServiceProvider Create(FakeTimeProvider time, IDistributedCache? sharedL2 = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        services.AddMemoryCache(options => options.Clock = new Clock(time));
        if (sharedL2 is not null)
            services.AddSingleton(sharedL2);
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private sealed class Clock(TimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
}
