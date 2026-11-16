using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Query;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.CityReports;

[Requirement("R3"), Requirement("R10")]
public sealed class CityReportServiceConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlyList<Place> ThreePlaces =
    [
        new("Athens", "GR", "Attica", new Coordinates(37.98, 23.73), "PPLC"),
        new("Piraeus", "GR", "Attica", new Coordinates(37.94, 23.65), "AIRP"),
        new("Chania", "GR", "Crete", new Coordinates(35.51, 24.02), "PPLA2")
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_StartsEveryLocationCallBeforeAnyCompletes()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new StartCounter();
        var service = CreateService(
            new GatedSource<GeocodingQuery, IReadOnlyList<Place>>(SourceNames.Geocoding, null, null, () => SourceResult.Ok(ThreePlaces)),
            new GatedSource<LocationQuery, Weather>(SourceNames.Weather, gate.Task, started, () => SourceResult.NoData<Weather>()),
            new GatedSource<LocationQuery, AirQuality>(SourceNames.AirQuality, gate.Task, started, () => SourceResult.NoData<AirQuality>()),
            new GatedSource<LocationQuery, Marine>(SourceNames.Marine, gate.Task, started, () => SourceResult.NoData<Marine>()));

        var report = service.GetAsync(Request(), Ct);
        var allStarted = await started.WaitForAsync(3 * ThreePlaces.Count, Timeout);

        Assert.True(allStarted, $"Only {started.Count} of {3 * ThreePlaces.Count} calls started before any was released: calls are not running in parallel.");
        Assert.False(report.IsCompleted);

        gate.SetResult();
        Assert.IsType<CityReportOutcome.Found>(await report);
    }

    [Fact]
    public async Task GetAsync_StartsNoLocationCallBeforeGeocodingCompletes()
    {
        var geocodingGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var geocodingStarted = new StartCounter();
        var locationStarted = new StartCounter();
        var service = CreateService(
            new GatedSource<GeocodingQuery, IReadOnlyList<Place>>(SourceNames.Geocoding, geocodingGate.Task, geocodingStarted, () => SourceResult.Ok(ThreePlaces)),
            new GatedSource<LocationQuery, Weather>(SourceNames.Weather, null, locationStarted, () => SourceResult.NoData<Weather>()),
            new GatedSource<LocationQuery, AirQuality>(SourceNames.AirQuality, null, locationStarted, () => SourceResult.NoData<AirQuality>()),
            new GatedSource<LocationQuery, Marine>(SourceNames.Marine, null, locationStarted, () => SourceResult.NoData<Marine>()));

        var report = service.GetAsync(Request(), Ct);
        Assert.True(await geocodingStarted.WaitForAsync(1, Timeout));
        Assert.Equal(0, locationStarted.Count);

        geocodingGate.SetResult();
        await report;

        Assert.Equal(3 * ThreePlaces.Count, locationStarted.Count);
    }

    private static CityReportRequest Request() =>
        new("Athens", "GR", ReportSectionsExtensions.DataSections, null, PlaceSort.Default, HourlyOptions.Default, new PlacePage(0, 100));

    private static CityReportService CreateService(
        IExternalSource<GeocodingQuery, IReadOnlyList<Place>> geocoding,
        IExternalSource<LocationQuery, Weather> weather,
        IExternalSource<LocationQuery, AirQuality> airQuality,
        IExternalSource<LocationQuery, Marine> marine) =>
        new(
            geocoding,
            weather,
            airQuality,
            marine,
            new UnusedHazards(),
            new RegionMatcher(Options.Create(new RegionMatchingOptions())),
            new DistanceCalculator(Options.Create(new DistanceOptions { ReferenceLatitude = 37.9715, ReferenceLongitude = 23.7257 })),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 15, 15, 20, 0, TimeSpan.Zero)),
            Options.Create(new CityReportOptions()));

    private sealed class UnusedHazards : IExternalSource<AreaQuery, Hazards>
    {
        public string Name => SourceNames.Hazards;

        public Task<SourceResult<Hazards>> FetchAsync(AreaQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Hazards are not part of these requests.");
    }

    private sealed class StartCounter
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Increment() => Interlocked.Increment(ref count);

        public async Task<bool> WaitForAsync(int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (Count < expected && DateTime.UtcNow < deadline)
                await Task.Delay(5, Ct);

            return Count >= expected;
        }
    }

    private sealed class GatedSource<TQuery, TResult>(
        string name,
        Task? gate,
        StartCounter? started,
        Func<SourceResult<TResult>> result) : IExternalSource<TQuery, TResult>
        where TQuery : ISourceQuery
        where TResult : class
    {
        public string Name => name;

        public async Task<SourceResult<TResult>> FetchAsync(TQuery query, CancellationToken cancellationToken)
        {
            started?.Increment();
            if (gate is not null)
                await gate.WaitAsync(cancellationToken);

            return result();
        }
    }
}
