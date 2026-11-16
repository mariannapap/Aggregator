using System.Net;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.Infrastructure.Options;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.OpenMeteo;

[Requirement("R2")]
public sealed class GeocodingSourceTests
{
    private static readonly GeocodingOptions Settings = new()
    {
        Count = 100,
        Language = "en",
        AllowedFeatureCodes = ["PPL", "PPLA", "PPLA2", "PPLA3", "PPLA4", "PPLC", "AIRP"]
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FetchAsync_KeepsSettlementsAndAirportsOnly()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("geocoding-athens-gr.json"));

        var result = await CreateSource(handler).FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Equal(["Athens", "Athens International Airport"], result.Value!.Select(place => place.Name));
        var athens = result.Value![0];
        Assert.Equal("GR", athens.Country);
        Assert.Equal("Attica", athens.Region);
        Assert.Equal(new Coordinates(37.98376, 23.72784), athens.Coordinates);
    }

    [Fact]
    public async Task FetchAsync_SendsNameCountryCountAndLanguage()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("geocoding-athens-gr.json"));

        await CreateSource(handler).FetchAsync(new GeocodingQuery("New York", "US"), Ct);

        Assert.Equal(
            "/v1/search?name=New%20York&countryCode=US&count=100&language=en&format=json",
            Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_NoResultsKey_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("geocoding-not-found.json"));

        var result = await CreateSource(handler).FetchAsync(new GeocodingQuery("Xyzqwerty", "GR"), Ct);

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_ServerError_ReturnsUnavailable()
    {
        var handler = FakeHttpHandler.Returning("{}", HttpStatusCode.InternalServerError);

        var result = await CreateSource(handler).FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_NetworkFailure_ReturnsUnavailable()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await CreateSource(handler).FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_ResultsWithoutCoordinates_AreSkippedAndLogged()
    {
        var handler = FakeHttpHandler.Returning("""
            {"results":[
              {"name":"Athens","latitude":37.98376,"longitude":23.72784,"feature_code":"PPLC","country_code":"GR","admin1":"Attica"},
              {"name":"No latitude","longitude":23.7,"feature_code":"PPL","country_code":"GR"},
              {"name":"Null longitude","latitude":37.9,"longitude":null,"feature_code":"PPL","country_code":"GR"}
            ]}
            """);
        var logger = new FakeLogger<GeocodingSource>();

        var result = await CreateSource(handler, logger).FetchAsync(new GeocodingQuery("Athens", "GR"), Ct);

        Assert.Equal("Athens", Assert.Single(result.Value!).Name);
        Assert.DoesNotContain(result.Value!, place => place.Coordinates == new Coordinates(0, 0));
        var warning = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Warning, 2004), (warning.Level, warning.Id.Id));
        Assert.Contains("2 result(s) without coordinates", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_OnlyResultsWithoutCoordinates_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning("""{"results":[{"name":"Nowhere","feature_code":"PPL","country_code":"GR"}]}""");

        var result = await CreateSource(handler).FetchAsync(new GeocodingQuery("Nowhere", "GR"), Ct);

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    private static GeocodingSource CreateSource(FakeHttpHandler handler, ILogger<GeocodingSource>? logger = null) =>
        new(handler.CreateClient("https://geocoding-api.open-meteo.com/"), Options.Create(Settings), logger ?? NullLogger<GeocodingSource>.Instance);
}
