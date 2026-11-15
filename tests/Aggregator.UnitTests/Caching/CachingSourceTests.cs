using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Caching;

public sealed class CachingSourceTests : IDisposable
{
    private readonly ServiceProvider provider = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly CountingSource inner = new();
    private readonly RecordingPolicy policy = new(TimeSpan.FromMinutes(5));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FetchAsync_SameQuery_CallsTheSourceOnce()
    {
        var source = CreateSource();

        await source.FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);
        var second = await source.FetchAsync(new GeocodingQuery(" athens ", "gr"), Ct);

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
    public void Name_IsTheInnerSourceName() => Assert.Equal(SourceNames.Geocoding, CreateSource().Name);

    public void Dispose() => provider.Dispose();

    private CachingSource<GeocodingQuery, IReadOnlyList<Place>> CreateSource() =>
        new(
            inner,
            new StaleAwareCache(provider.GetRequiredService<HybridCache>()),
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
