using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class StaleAwareCacheTests : IDisposable
{
    private static readonly Weather Sample = new(new CurrentWeather(18, 5), []);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider provider;
    private readonly StaleAwareCache cache;

    public StaleAwareCacheTests()
    {
        provider = FakeClockCache.Create(time);
        cache = new StaleAwareCache(provider.GetRequiredService<HybridCache>(), time, NullLogger<StaleAwareCache>.Instance);
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
    [Requirement("R6"), FailureScenario]
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
    [Requirement("R6"), FailureScenario]
    public async Task GetAsync_FailureAfterExpiry_ServesLastGoodAsStale()
    {
        var freshFor = TimeSpan.FromMinutes(1);
        await cache.GetAsync("flaky", _ => Task.FromResult(SourceResult.Ok(Sample)), freshFor, TimeSpan.FromHours(1), Ct);
        time.Advance(TimeSpan.FromMinutes(2));

        var result = await cache.GetAsync("flaky", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), freshFor, TimeSpan.FromHours(1), Ct);

        Assert.Equal(SourceStatus.Stale, result.Status);
        Assert.Equal(Sample.Current, result.Value!.Current);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task GetAsync_FailureAfterExpiredNoData_ReturnsUnavailableNotStale()
    {
        var freshFor = TimeSpan.FromMinutes(1);
        await cache.GetAsync("nothing", _ => Task.FromResult(SourceResult.NoData<Weather>()), freshFor, TimeSpan.FromHours(1), Ct);
        time.Advance(TimeSpan.FromMinutes(2));

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

    [Fact]
    public async Task GetAsync_StampsTheFetchTime_AndACacheHitKeepsIt()
    {
        var fetchedAt = time.GetUtcNow();
        await cache.GetAsync("stamped", _ => Task.FromResult(SourceResult.Ok(Sample)), TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);
        time.Advance(TimeSpan.FromMinutes(1));

        var hit = await cache.GetAsync("stamped", _ => Task.FromResult(SourceResult.Ok(Sample)), TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), Ct);

        Assert.Equal(fetchedAt, hit.FetchedAt);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task GetAsync_StaleValue_KeepsTheTimeItWasOriginallyFetched()
    {
        var fetchedAt = time.GetUtcNow();
        await cache.GetAsync("aging", _ => Task.FromResult(SourceResult.Ok(Sample)), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), Ct);
        time.Advance(TimeSpan.FromMinutes(30));

        var stale = await cache.GetAsync("aging", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), Ct);

        Assert.Equal((SourceStatus.Stale, fetchedAt), (stale.Status, stale.FetchedAt));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task GetAsync_Unavailable_HasNoFetchTime() =>
        Assert.Null((await cache.GetAsync("down", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), Ct)).FetchedAt);

    [Fact]
    public async Task HybridCacheReadOnlyLookup_OnAMiss_StoresNothing()
    {
        var hybrid = provider.GetRequiredService<HybridCache>();
        var readOnly = new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableUnderlyingData };

        var miss = await hybrid.GetOrCreateAsync<string?>("missing", _ => ValueTask.FromResult<string?>("never called"), readOnly, cancellationToken: Ct);
        var afterMiss = await hybrid.GetOrCreateAsync<string?>("missing", _ => ValueTask.FromResult<string?>("fetched"), cancellationToken: Ct);

        Assert.Null(miss);
        Assert.Equal("fetched", afterMiss);
    }

    public void Dispose() => provider.Dispose();
}
