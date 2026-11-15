using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Query;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.CityReports;

public sealed class CityReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 15, 20, 0, TimeSpan.Zero);
    private static readonly Place Athens = new("Athens", "GR", "Attica", new Coordinates(37.98376, 23.72784));
    private static readonly Place Airport = new("Athens International Airport", "GR", "Attica", new Coordinates(37.93636, 23.94447));
    private static readonly Place Chania = new("Chania", "GR", "Crete", new Coordinates(35.51, 24.02));

    private readonly FakeSource<GeocodingQuery, IReadOnlyList<Place>> geocoding = new(SourceNames.Geocoding);
    private readonly FakeSource<LocationQuery, Weather> weather = new(SourceNames.Weather);
    private readonly FakeSource<LocationQuery, AirQuality> airQuality = new(SourceNames.AirQuality);
    private readonly FakeSource<LocationQuery, Marine> marine = new(SourceNames.Marine);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_GeocodingUnavailable_ReturnsGeocodingUnavailable()
    {
        geocoding.Respond = _ => SourceResult.Unavailable<IReadOnlyList<Place>>();

        var outcome = await CreateService().GetAsync(Request(), Ct);

        Assert.IsType<CityReportOutcome.GeocodingUnavailable>(outcome);
        Assert.Empty(weather.Queries);
    }

    [Fact]
    public async Task GetAsync_NoPlaces_ReturnsCityNotFound()
    {
        geocoding.Respond = _ => SourceResult.NoData<IReadOnlyList<Place>>();

        var outcome = await CreateService().GetAsync(Request(), Ct);

        Assert.IsType<CityReportOutcome.CityNotFound>(outcome);
    }

    [Fact]
    public async Task GetAsync_StaleGeocoding_StillBuildsReportAndReportsStatus()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]).AsStale();
        weather.Respond = _ => SourceResult.Ok(new Weather(new CurrentWeather(18, 5), []));
        airQuality.Respond = _ => SourceResult.Unavailable<AirQuality>();
        marine.Respond = _ => SourceResult.Ok(new Marine(new CurrentMarine(0.1, 20.5), []));

        var report = Found(await CreateService().GetAsync(Request(), Ct));

        var place = Assert.Single(report.Places);
        Assert.Equal(SourceStatus.Stale, place.GeocodingStatus);
        Assert.Equal(SourceStatus.Ok, place.Weather!.Status);
        Assert.Equal(SourceStatus.Unavailable, place.AirQuality!.Status);
        Assert.Equal(SourceStatus.Ok, place.Marine!.Status);
    }

    [Fact]
    public async Task GetAsync_DefaultRequest_CallsEveryLocationSourcePerPlaceWithoutHourly()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens, Airport]);

        await CreateService().GetAsync(Request(), Ct);

        Assert.Equal((2, 2, 2), (weather.Queries.Count, airQuality.Queries.Count, marine.Queries.Count));
        Assert.All(
            [.. weather.Queries, .. airQuality.Queries, .. marine.Queries],
            query => Assert.True(query.IncludesCurrent && !query.IncludesHourly));
    }

    [Fact]
    public async Task GetAsync_Marine_MapsCurrentValuesPerPlace()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens, Airport]);
        marine.Respond = _ => SourceResult.Ok(new Marine(new CurrentMarine(0.1, 20.5), []));

        var report = Found(await CreateService().GetAsync(Request(ReportSections.Marine), Ct));

        Assert.Empty(weather.Queries);
        Assert.Empty(airQuality.Queries);
        Assert.All(report.Places, place => Assert.Equal(new CurrentMarine(0.1, 20.5), place.Marine!.Value!.Current));
    }

    [Fact]
    public async Task GetAsync_MarineNoDataInland_KeepsOtherSectionsAndReportsNoData()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);
        weather.Respond = _ => SourceResult.Ok(new Weather(new CurrentWeather(18, 5), []));
        marine.Respond = _ => SourceResult.NoData<Marine>();

        var place = Assert.Single(Found(await CreateService().GetAsync(Request(ReportSections.Weather | ReportSections.Marine), Ct)).Places);

        Assert.Equal(SourceStatus.Ok, place.Weather!.Status);
        Assert.Equal(SourceStatus.NoData, place.Marine!.Status);
    }

    [Fact]
    public async Task GetAsync_HourlyMarineOnly_CallsOnlyMarineForHourlyRows()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);
        var start = new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
        marine.Respond = _ => SourceResult.Ok(new Marine(null, [new HourlyReading(start, HourlyCategory.Marine, "waveHeight", 0.1, "m")]));

        var report = Found(await CreateService().GetAsync(
            Request(ReportSections.Hourly, hourly: HourlyOptions.Default with { Category = HourlyCategory.Marine }),
            Ct));

        Assert.Empty(weather.Queries);
        Assert.Empty(airQuality.Queries);
        var marineQuery = Assert.Single(marine.Queries);
        Assert.False(marineQuery.IncludesCurrent);
        Assert.Equal(HourlyCategory.Marine, Assert.Single(Assert.Single(report.Places).Hourly!).Category);
    }

    [Fact]
    public async Task GetAsync_HourlyCategory_RestrictsHourlyRowsToThatSection()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);

        await CreateService().GetAsync(
            Request(ReportSections.Weather | ReportSections.AirQuality | ReportSections.Hourly, hourly: HourlyOptions.Default with { Category = HourlyCategory.AirQuality }),
            Ct);

        var weatherQuery = Assert.Single(weather.Queries);
        Assert.True(weatherQuery.IncludesCurrent);
        Assert.False(weatherQuery.IncludesHourly);
        var airQualityQuery = Assert.Single(airQuality.Queries);
        Assert.True(airQualityQuery.IncludesCurrent);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero), airQualityQuery.Hourly!.Start);
        Assert.Empty(marine.Queries);
    }

    [Fact]
    public async Task GetAsync_HourlyWithSections_FetchesHourlyOnlyForIncludedSections()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);

        await CreateService().GetAsync(Request(ReportSections.Weather | ReportSections.AirQuality | ReportSections.Hourly), Ct);

        Assert.True(Assert.Single(weather.Queries).IncludesHourly);
        Assert.True(Assert.Single(airQuality.Queries).IncludesHourly);
        Assert.Empty(marine.Queries);
    }

    [Fact]
    public async Task GetAsync_HourlyOnly_FetchesHourlyFromEverySection()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);

        await CreateService().GetAsync(Request(ReportSections.Hourly), Ct);

        Assert.All(
            [Assert.Single(weather.Queries), Assert.Single(airQuality.Queries), Assert.Single(marine.Queries)],
            query => Assert.True(!query.IncludesCurrent && query.IncludesHourly));
    }

    [Fact]
    public async Task GetAsync_WeatherNotIncluded_DoesNotCallWeather()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);

        var report = Found(await CreateService().GetAsync(Request(ReportSections.AirQuality), Ct));

        Assert.Empty(weather.Queries);
        Assert.Null(Assert.Single(report.Places).Weather);
    }

    [Fact]
    public async Task GetAsync_RegionMatches_FiltersPlacesBeforeCallingSources()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens, Chania]);

        var report = Found(await CreateService().GetAsync(Request(region: "krete"), Ct));

        Assert.Equal("Chania", Assert.Single(report.Places).Place.Name);
        Assert.Single(weather.Queries);
        Assert.Equal(["Crete"], report.Filters.Region!.Matched);
    }

    [Fact]
    public async Task GetAsync_RegionDoesNotMatch_IgnoresFilterAndReportsIt()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens, Chania]);

        var report = Found(await CreateService().GetAsync(Request(region: "xyz"), Ct));

        Assert.Equal(2, report.Places.Count);
        Assert.False(report.Filters.Region!.Applied);
    }

    [Fact]
    public async Task GetAsync_DefaultSort_IsNearestToReferenceFirst()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Chania, Airport, Athens]);

        var report = Found(await CreateService().GetAsync(Request(), Ct));

        Assert.Equal(["Athens", "Athens International Airport", "Chania"], report.Places.Select(place => place.Place.Name));
    }

    [Fact]
    public async Task GetAsync_SortByTemperatureDescending_PutsMissingTemperaturesLast()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens, Airport, Chania]);
        weather.Respond = query => query.Coordinates.Latitude switch
        {
            37.98 => SourceResult.Ok(new Weather(new CurrentWeather(18, 5), [])),
            35.51 => SourceResult.Ok(new Weather(new CurrentWeather(21, 5), [])),
            _ => SourceResult.Unavailable<Weather>()
        };

        var report = Found(await CreateService().GetAsync(Request(sort: new PlaceSort(PlaceSortField.Temperature, true)), Ct));

        Assert.Equal(["Chania", "Athens", "Athens International Airport"], report.Places.Select(place => place.Place.Name));
    }

    [Fact]
    public async Task GetAsync_Hourly_MergesFiltersAndSortsRows()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);
        var start = new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
        weather.Respond = _ => SourceResult.Ok(new Weather(null,
            [.. Enumerable.Range(0, 24).Select(hour => new HourlyReading(start.AddHours(hour), HourlyCategory.Weather, "temperature", hour, "°C"))]));
        airQuality.Respond = _ => SourceResult.Ok(new AirQuality(null,
            [.. Enumerable.Range(0, 24).Select(hour => new HourlyReading(start.AddHours(hour), HourlyCategory.AirQuality, "usAqi", 50, "USAQI"))]));
        marine.Respond = _ => SourceResult.Ok(new Marine(null,
            [.. Enumerable.Range(0, 24).Select(hour => new HourlyReading(start.AddHours(hour), HourlyCategory.Marine, "waveHeight", 0.2, "m"))]));

        var hourly = HourlyOptions.Default with { From = start.AddHours(2), To = start.AddHours(3), SortDescending = true };
        var report = Found(await CreateService().GetAsync(Request(ReportSections.Hourly, hourly: hourly), Ct));

        var rows = Assert.Single(report.Places).Hourly!;
        Assert.Equal(6, rows.Count);
        Assert.Equal(start.AddHours(3), rows[0].Time);
        Assert.Equal(start.AddHours(2), rows[^1].Time);
        Assert.Equal(
            [HourlyCategory.Weather, HourlyCategory.AirQuality, HourlyCategory.Marine],
            rows.Take(3).Select(row => row.Category));
        Assert.False(report.Filters.HourlyRange!.Clamped);
    }

    [Fact]
    public async Task GetAsync_HourlyRangeOutsideWindow_IsClampedAndReported()
    {
        geocoding.Respond = _ => SourceResult.Ok<IReadOnlyList<Place>>([Athens]);
        var hourly = HourlyOptions.Default with { From = Now.AddDays(-1), To = Now.AddDays(3) };

        var report = Found(await CreateService().GetAsync(Request(ReportSections.Hourly, hourly: hourly), Ct));

        var range = report.Filters.HourlyRange!;
        Assert.True(range.Clamped);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero), range.AppliedFrom);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero), range.AppliedTo);
    }

    private static CityReport Found(CityReportOutcome outcome) => Assert.IsType<CityReportOutcome.Found>(outcome).Report;

    private static CityReportRequest Request(
        ReportSections sections = ReportSectionsExtensions.DataSections,
        string? region = null,
        PlaceSort? sort = null,
        HourlyOptions? hourly = null) =>
        new("Athens", "GR", sections, region, sort ?? PlaceSort.Default, hourly ?? HourlyOptions.Default);

    private CityReportService CreateService() =>
        new(
            geocoding,
            weather,
            airQuality,
            marine,
            new RegionMatcher(Options.Create(new RegionMatchingOptions())),
            new RelevanceCalculator(Options.Create(new RelevanceOptions { ReferenceLatitude = 37.9715, ReferenceLongitude = 23.7257 })),
            new FakeTimeProvider(Now),
            Options.Create(new CityReportOptions()));

    private sealed class FakeSource<TQuery, TResult>(string name) : IExternalSource<TQuery, TResult>
        where TQuery : ISourceQuery
        where TResult : class
    {
        public Func<TQuery, SourceResult<TResult>> Respond { get; set; } = _ => SourceResult.NoData<TResult>();

        public List<TQuery> Queries { get; } = [];

        public string Name => name;

        public Task<SourceResult<TResult>> FetchAsync(TQuery query, CancellationToken cancellationToken)
        {
            lock (Queries)
                Queries.Add(query);
            return Task.FromResult(Respond(query));
        }
    }
}
