using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class CachingSourceTests : IDisposable
{
    private readonly ServiceProvider provider = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly CountingSource inner = new();
    private readonly RecordingPolicy policy = new(TimeSpan.FromMinutes(5));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FetchAsync_SameQueryInAnotherCase_CallsTheSourceOnce()
    {
        var source = CreateSource();

        await source.FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);
        var second = await source.FetchAsync(new GeocodingQuery("ATHENS", "GR"), Ct);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(SourceStatus.Ok, second.Status);
    }

    [Fact]
    public async Task FetchAsync_DifferentQueries_AreCachedSeparately()
    {
        var source = CreateSource();

        await source.FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);
        await source.FetchAsync(new GeocodingQuery("Athens", "US"), Ct);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FetchAsync_AsksThePolicyForTheTtlOfTheQuery()
    {
        var query = new GeocodingQuery("Athens", "GR");

        await CreateSource().FetchAsync(query, Ct);

        Assert.Equal(query, Assert.Single(policy.Queries));
    }

    [Fact]
    public async Task FetchAsync_ConcurrentIdenticalQueries_CallTheSourceOnce()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowInner = new GatedCountingSource(gate.Task);
        var source = new CachingSource<GeocodingQuery, IReadOnlyList<Place>>(
            slowInner,
            new StaleAwareCache(provider.GetRequiredService<HybridCache>(), NullLogger<StaleAwareCache>.Instance),
            policy,
            new StaticOptionsMonitor(new SourceClientOptions { LastGoodRetention = TimeSpan.FromHours(1) }));

        var requests = Enumerable.Range(0, 20).Select(_ => source.FetchAsync(new GeocodingQuery("Athens", "GR"), Ct)).ToList();
        await Task.Delay(100, Ct);
        gate.SetResult();
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, slowInner.Calls);
        Assert.All(results, result => Assert.Equal(SourceStatus.Ok, result.Status));
    }

    [Fact]
    public async Task FetchAsync_SameQueryThroughDifferentSources_IsCachedSeparately()
    {
        var sharedCache = new StaleAwareCache(provider.GetRequiredService<HybridCache>(), NullLogger<StaleAwareCache>.Instance);
        var options = new StaticOptionsMonitor(new SourceClientOptions { CacheTtl = TimeSpan.FromMinutes(5), LastGoodRetention = TimeSpan.FromHours(1) });
        var weatherInner = new NamedWeatherSource(SourceNames.Weather, 18);
        var marineInner = new NamedWeatherSource(SourceNames.Marine, 99);
        var weather = new CachingSource<LocationQuery, Weather>(weatherInner, sharedCache, new LocationCachePolicy(), options);
        var marine = new CachingSource<LocationQuery, Weather>(marineInner, sharedCache, new LocationCachePolicy(), options);
        var query = new LocationQuery(new Coordinates(37.98, 23.73), 2, includeCurrent: true, hourly: null);

        var fromWeather = await weather.FetchAsync(query, Ct);
        var fromMarine = await marine.FetchAsync(query, Ct);
        var weatherAgain = await weather.FetchAsync(query, Ct);

        Assert.Equal((1, 1), (weatherInner.Calls, marineInner.Calls));
        Assert.Equal(18, fromWeather.Value!.Current!.Temperature);
        Assert.Equal(99, fromMarine.Value!.Current!.Temperature);
        Assert.Equal(18, weatherAgain.Value!.Current!.Temperature);
    }

    [Fact]
    public void Name_IsTheInnerSourceName() => Assert.Equal(SourceNames.Geocoding, CreateSource().Name);

    public void Dispose() => provider.Dispose();

    private CachingSource<GeocodingQuery, IReadOnlyList<Place>> CreateSource() =>
        new(
            inner,
            new StaleAwareCache(provider.GetRequiredService<HybridCache>(), NullLogger<StaleAwareCache>.Instance),
            policy,
            new StaticOptionsMonitor(new SourceClientOptions { LastGoodRetention = TimeSpan.FromHours(1) }));

    private sealed class CountingSource : IExternalSource<GeocodingQuery, IReadOnlyList<Place>>
    {
        public int Calls { get; private set; }

        public string Name => SourceNames.Geocoding;

        public Task<SourceResult<IReadOnlyList<Place>>> FetchAsync(GeocodingQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(SourceResult.Ok<IReadOnlyList<Place>>([new Place(query.City, query.Country, null, new Coordinates(1, 1))]));
        }
    }

    private sealed class GatedCountingSource(Task gate) : IExternalSource<GeocodingQuery, IReadOnlyList<Place>>
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public string Name => SourceNames.Geocoding;

        public async Task<SourceResult<IReadOnlyList<Place>>> FetchAsync(GeocodingQuery query, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await gate.WaitAsync(cancellationToken);
            return SourceResult.Ok<IReadOnlyList<Place>>([new Place(query.City, query.Country, null, new Coordinates(1, 1))]);
        }
    }

    private sealed class NamedWeatherSource(string name, double temperature) : IExternalSource<LocationQuery, Weather>
    {
        public int Calls { get; private set; }

        public string Name => name;

        public Task<SourceResult<Weather>> FetchAsync(LocationQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(SourceResult.Ok(new Weather(new CurrentWeather(temperature, 1), [])));
        }
    }

    private sealed class RecordingPolicy(TimeSpan ttl) : ICachePolicy<GeocodingQuery>
    {
        public List<GeocodingQuery> Queries { get; } = [];

        public TimeSpan FreshFor(GeocodingQuery query, SourceClientOptions settings)
        {
            Queries.Add(query);
            return ttl;
        }
    }

    private sealed class StaticOptionsMonitor(SourceClientOptions value) : IOptionsMonitor<SourceClientOptions>
    {
        public SourceClientOptions CurrentValue => value;

        public SourceClientOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SourceClientOptions, string?> listener) => null;
    }
}
