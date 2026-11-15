using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class CacheRoundTripTests : IDisposable
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan KeepLastGood = TimeSpan.FromHours(1);
    private static readonly DateTimeOffset Hour = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly Weather Rich = new(
        new CurrentWeather(18.5, 7.2),
        [
            new HourlyReading(Hour, HourlyCategory.Weather, "temperature", 18.5, "°C"),
            new HourlyReading(Hour.AddHours(1), HourlyCategory.Weather, "windSpeed", 7.2, "km/h"),
            new HourlyReading(Hour.AddHours(2), HourlyCategory.Marine, "waveHeight", 0.3, "m")
        ]);

    private static readonly IReadOnlyList<Place> Places =
    [
        new("Athens", "GR", "Attica", new Coordinates(37.98376, 23.72784)),
        new("Nowhere", "GR", null, new Coordinates(-12.5, 0.000001))
    ];

    private readonly InMemoryDistributedCache sharedL2 = new(TimeProvider.System);
    private readonly ServiceProvider writerProvider;
    private readonly ServiceProvider readerProvider;
    private readonly StaleAwareCache writer;
    private readonly StaleAwareCache reader;

    public CacheRoundTripTests()
    {
        writerProvider = CreateProvider(sharedL2);
        readerProvider = CreateProvider(sharedL2);
        writer = new StaleAwareCache(writerProvider.GetRequiredService<HybridCache>());
        reader = new StaleAwareCache(readerProvider.GetRequiredService<HybridCache>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CachedPartial_StaysPartialOnACacheHit()
    {
        var calls = 0;
        Task<SourceResult<Weather>> Fetch(CancellationToken _)
        {
            calls++;
            return Task.FromResult(SourceResult.Partial(Rich));
        }

        await writer.GetAsync("partial", Fetch, TimeSpan.FromMinutes(5), KeepLastGood, Ct);
        var hit = await writer.GetAsync("partial", Fetch, TimeSpan.FromMinutes(5), KeepLastGood, Ct);

        Assert.Equal(1, calls);
        Assert.Equal(SourceStatus.Partial, hit.Status);
    }

    [Fact]
    public async Task FullWeatherGraph_SurvivesSerializationThroughTheSharedStore()
    {
        await writer.GetAsync("weather", _ => Task.FromResult(SourceResult.Partial(Rich)), TimeSpan.FromMinutes(5), KeepLastGood, Ct);

        var read = await reader.GetAsync<Weather>("weather", _ => throw new InvalidOperationException("must be a cache hit"), TimeSpan.FromMinutes(5), KeepLastGood, Ct);

        Assert.True(sharedL2.Count > 0, "Nothing reached the shared store, so nothing was serialized.");
        Assert.Equal(SourceStatus.Partial, read.Status);
        AssertSameWeather(Rich, read.Value!);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FullWeatherGraph_SurvivesTheLastGoodPathThroughTheSharedStore()
    {
        await writer.GetAsync("weather", _ => Task.FromResult(SourceResult.Ok(Rich)), FreshFor, KeepLastGood, Ct);
        await Task.Delay(FreshFor * 3, Ct);

        var stale = await reader.GetAsync("weather", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), FreshFor, KeepLastGood, Ct);

        Assert.Equal(SourceStatus.Stale, stale.Status);
        AssertSameWeather(Rich, stale.Value!);
    }

    [Fact]
    public async Task PlaceList_WithMissingRegionAndExtremeCoordinates_SurvivesTheSharedStore()
    {
        await writer.GetAsync("places", _ => Task.FromResult(SourceResult.Ok(Places)), TimeSpan.FromMinutes(5), KeepLastGood, Ct);

        var read = await reader.GetAsync<IReadOnlyList<Place>>("places", _ => throw new InvalidOperationException("must be a cache hit"), TimeSpan.FromMinutes(5), KeepLastGood, Ct);

        Assert.Equal(Places, read.Value!);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task ServingStale_DoesNotExtendTheLastGoodRetention()
    {
        var retention = TimeSpan.FromMilliseconds(400);
        var freshFor = TimeSpan.FromMilliseconds(50);
        await writer.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Rich)), freshFor, retention, Ct);
        Task<SourceResult<Weather>> Failing(CancellationToken _) => Task.FromResult(SourceResult.Unavailable<Weather>());

        var served = new List<SourceStatus>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
            served.Add((await writer.GetAsync("key", Failing, freshFor, retention, Ct)).Status);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(400), Ct);
        var afterRetention = await writer.GetAsync("key", Failing, freshFor, retention, Ct);

        Assert.All(served, status => Assert.Equal(SourceStatus.Stale, status));
        Assert.Equal(SourceStatus.Unavailable, afterRetention.Status);
    }

    public void Dispose()
    {
        writerProvider.Dispose();
        readerProvider.Dispose();
    }

    private static ServiceProvider CreateProvider(IDistributedCache sharedL2)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sharedL2);
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static void AssertSameWeather(Weather expected, Weather actual)
    {
        Assert.Equal(expected.Current, actual.Current);
        Assert.Equal(expected.Hourly, actual.Hourly);
        Assert.All(actual.Hourly, reading => Assert.Equal(TimeSpan.Zero, reading.Time.Offset));
    }
}
