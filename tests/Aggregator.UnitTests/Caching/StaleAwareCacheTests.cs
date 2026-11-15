using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.UnitTests.Caching;

public sealed class StaleAwareCacheTests : IDisposable
{
    private static readonly Weather Sample = new(new CurrentWeather(18, 5), []);

    private readonly ServiceProvider provider;
    private readonly StaleAwareCache cache;

    public StaleAwareCacheTests()
    {
        provider = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        cache = new StaleAwareCache(provider.GetRequiredService<HybridCache>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_FreshValue_IsFetchedOnce()
    {
        var calls = 0;
        Task<SourceResult<Weather>> Fetch(CancellationToken _)
        {
            calls++;
            return Task.FromResult(SourceResult.Ok(Sample));
        }

        await cache.GetAsync("key", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        var second = await cache.GetAsync("key", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);

        Assert.Equal(1, calls);
        Assert.Equal(SourceStatus.Ok, second.Status);
    }

    [Fact]
    public async Task GetAsync_CacheHits_ShareOneImmutableInstance()
    {
        Task<SourceResult<Weather>> Fetch(CancellationToken _) => Task.FromResult(SourceResult.Ok(new Weather(new CurrentWeather(18, 5), [])));

        await cache.GetAsync("shared", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        var firstHit = await cache.GetAsync("shared", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        var secondHit = await cache.GetAsync("shared", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);

        Assert.Same(firstHit, secondHit);
    }

    [Fact]
    public async Task GetAsync_UnavailableWithoutHistory_IsNotCachedAndReturnsUnavailable()
    {
        var calls = 0;
        Task<SourceResult<Weather>> Fetch(CancellationToken _)
        {
            calls++;
            return Task.FromResult(SourceResult.Unavailable<Weather>());
        }

        var first = await cache.GetAsync("down", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        await cache.GetAsync("down", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);

        Assert.Equal(SourceStatus.Unavailable, first.Status);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetAsync_FailureAfterExpiry_ServesLastGoodAsStale()
    {
        var freshFor = TimeSpan.FromMilliseconds(50);
        await cache.GetAsync("flaky", _ => Task.FromResult(SourceResult.Ok(Sample)), freshFor, TimeSpan.FromHours(1), Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(150), Ct);

        var result = await cache.GetAsync("flaky", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), freshFor, TimeSpan.FromHours(1), Ct);

        Assert.Equal(SourceStatus.Stale, result.Status);
        Assert.Equal(Sample.Current, result.Value!.Current);
    }

    [Fact]
    public async Task GetAsync_FailureAfterExpiredNoData_ReturnsUnavailableNotStale()
    {
        var freshFor = TimeSpan.FromMilliseconds(50);
        await cache.GetAsync("nothing", _ => Task.FromResult(SourceResult.NoData<Weather>()), freshFor, TimeSpan.FromHours(1), Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(150), Ct);

        var result = await cache.GetAsync("nothing", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), freshFor, TimeSpan.FromHours(1), Ct);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task GetAsync_NoData_IsCachedLikeAnyAnswer()
    {
        var calls = 0;
        Task<SourceResult<Weather>> Fetch(CancellationToken _)
        {
            calls++;
            return Task.FromResult(SourceResult.NoData<Weather>());
        }

        await cache.GetAsync("empty", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        var second = await cache.GetAsync("empty", Fetch, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);

        Assert.Equal(1, calls);
        Assert.Equal(SourceStatus.NoData, second.Status);
    }

    public void Dispose() => provider.Dispose();
}
