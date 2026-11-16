using System.Globalization;
using System.Net;
using System.Web;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Eonet;
using Aggregator.Infrastructure.Options;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Eonet;

[Requirement("R2")]
public sealed class HazardsSourceTests
{
    private static readonly HazardsOptions Settings = new() { RadiusKm = 150, Days = 30, Limit = 50 };
    private static readonly Coordinates Athens = new(37.98, 23.73);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void BuildUri_AsksForRecentOpenEvents_InABoxWestNorthEastSouth()
    {
        var query = HttpUtility.ParseQueryString(new Uri(new Uri("https://eonet.gsfc.nasa.gov/"), HazardsSource.BuildUri(Athens, Settings)).Query);

        Assert.Equal(("open", "30", "50"), (query["status"], query["days"], query["limit"]));
        var box = query["bbox"]!.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var (west, north, east, south) = (box[0], box[1], box[2], box[3]);
        Assert.True(west < Athens.Longitude && Athens.Longitude < east, "longitudes");
        Assert.True(south < Athens.Latitude && Athens.Latitude < north, "latitudes");
        Assert.InRange(new Coordinates(north, Athens.Longitude).DistanceKmTo(Athens), 149, 151);
        Assert.InRange(new Coordinates(Athens.Latitude, east).DistanceKmTo(Athens), 149, 151);
    }

    [Fact]
    public async Task FetchAsync_KeepsEventsWithinTheRadius_NearestFirst_AtTheirLatestPosition()
    {
        var source = Source("""
            {"events":[
              {"title":"Far fire","categories":[{"title":"Wildfires"}],"geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[24.73,37.98]}]},
              {"title":"Moving storm","categories":[{"title":"Severe Storms"}],"geometry":[
                {"date":"2026-11-14T10:00:00Z","type":"Point","coordinates":[30.0,37.98]},
                {"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[23.83,37.98]}]},
              {"title":"Outside the radius","categories":[{"title":"Volcanoes"}],"geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[28.0,37.98]}]}
            ]}
            """);

        var result = await source.FetchAsync(new AreaQuery(Athens, 2), Ct);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Equal(["Moving storm", "Far fire"], result.Value!.Events.Select(hazard => hazard.Title));
        Assert.Equal("Severe Storms", result.Value.Events[0].Category);
        Assert.InRange(result.Value.Events[0].DistanceKm, 8, 10);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero), result.Value.Events[0].Date);
    }

    [Fact]
    public async Task FetchAsync_Polygon_IsPlacedAtTheAverageOfItsOuterRing()
    {
        var source = Source("""
            {"events":[{"title":"Flood","categories":[{"title":"Floods"}],"geometry":[{"date":"2026-11-15T10:00:00Z","type":"Polygon",
              "coordinates":[[[23.63,37.88],[23.83,37.88],[23.83,38.08],[23.63,38.08]]]}]}]}
            """);

        var result = await source.FetchAsync(new AreaQuery(Athens, 2), Ct);

        Assert.InRange(Assert.Single(result.Value!.Events).DistanceKm, 0, 1);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_MalformedEvents_AreSkipped_NotFatal()
    {
        var source = Source("""
            {"events":[
              {"title":"","geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[23.73,37.98]}]},
              {"title":"No position","geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":["x","y"]}]},
              {"title":"No geometry"},
              {"title":"Unknown category","geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[23.73,37.98]}]}
            ]}
            """);

        var result = await source.FetchAsync(new AreaQuery(Athens, 2), Ct);

        var hazard = Assert.Single(result.Value!.Events);
        Assert.Equal(("Unknown category", "Other"), (hazard.Title, hazard.Category));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_NoEventsNearby_IsNoData() =>
        Assert.Equal(SourceStatus.NoData, (await Source("""{"events":[]}""").FetchAsync(new AreaQuery(Athens, 2), Ct)).Status);

    [Theory]
    [InlineData("{}", HttpStatusCode.InternalServerError)]
    [InlineData("not json", HttpStatusCode.OK)]
    [InlineData("""{"title":"no events array"}""", HttpStatusCode.OK)]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_ServerErrorOrBrokenPayload_IsUnavailable(string body, HttpStatusCode status) =>
        Assert.Equal(SourceStatus.Unavailable, (await Source(body, status).FetchAsync(new AreaQuery(Athens, 2), Ct)).Status);

    [Fact]
    public void AreaQuery_KeyFollowsTheRoundedCentre()
    {
        Assert.Equal(new AreaQuery(new Coordinates(37.981, 23.729), 2).CacheKey, new AreaQuery(new Coordinates(37.979, 23.731), 2).CacheKey);
        Assert.NotEqual(new AreaQuery(new Coordinates(37.98, 23.73), 2).CacheKey, new AreaQuery(new Coordinates(38.98, 23.73), 2).CacheKey);
    }

    private static HazardsSource Source(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(FakeHttpHandler.Returning(body, status).CreateClient("https://eonet.gsfc.nasa.gov/"), Options.Create(Settings), NullLogger<HazardsSource>.Instance);
}
