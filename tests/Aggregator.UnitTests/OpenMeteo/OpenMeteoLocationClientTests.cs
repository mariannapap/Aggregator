using System.Globalization;
using System.Net;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aggregator.UnitTests.OpenMeteo;

[Requirement("R2")]
public sealed class OpenMeteoLocationClientTests
{
    private static readonly OpenMeteoEndpoint Endpoint = new(
        "test",
        "v1/test",
        HourlyCategory.Weather,
        [new("field_a", "metricA", "unitA"), new("field_b", "metricB", "unitB")]);

    private static readonly HourlyWindow Window = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero), 24);
    private static readonly Coordinates Location = new(37.98376, 23.72784);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, false, "v1/test?latitude=37.98&longitude=23.73&timezone=GMT&current=field_a,field_b")]
    [InlineData(false, true, "v1/test?latitude=37.98&longitude=23.73&timezone=GMT&hourly=field_a,field_b&start_hour=2026-11-15T15:00&end_hour=2026-11-16T14:00")]
    [InlineData(true, true, "v1/test?latitude=37.98&longitude=23.73&timezone=GMT&current=field_a,field_b&hourly=field_a,field_b&start_hour=2026-11-15T15:00&end_hour=2026-11-16T14:00")]
    public void BuildUri_RequestsOnlyWhatTheQueryNeeds(bool includeCurrent, bool includeHourly, string expected) =>
        Assert.Equal(expected, OpenMeteoLocationClient.BuildUri(Endpoint, Query(includeCurrent, includeHourly)));

    [Fact]
    public async Task FetchAsync_CompleteCurrentAndHourly_ReturnsOk()
    {
        var result = await Fetch(Json(current: """{"field_a":1.5,"field_b":2}""", hours: 24), Query(true, true));

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Equal(new TestCurrent(1.5, 2), result.Value!.Current);
        Assert.Equal(48, result.Value.Hourly.Count);
        Assert.Contains(result.Value.Hourly, reading => reading is { Metric: "metricA", Unit: "unitA", Category: HourlyCategory.Weather });
    }

    [Fact]
    public async Task FetchAsync_HourlyUnitsFromResponse_OverrideDefaults()
    {
        var result = await Fetch(Json(current: null, hours: 24, units: """{"field_a":"°F","field_b":"mph"}"""), Query(false, true));

        Assert.Contains(result.Value!.Hourly, reading => reading is { Metric: "metricA", Unit: "°F" });
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_MissingCurrentField_WithHourly_ReturnsPartial()
    {
        var result = await Fetch(Json(current: """{"field_a":1.5}""", hours: 24), Query(true, true));

        Assert.Equal(SourceStatus.Partial, result.Status);
        Assert.Null(result.Value!.Current);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_MissingHourlyRows_ReturnsPartial()
    {
        var result = await Fetch(Json(current: null, hours: 20), Query(false, true));

        Assert.Equal(SourceStatus.Partial, result.Status);
        Assert.Equal(40, result.Value!.Hourly.Count);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_NothingUsable_ReturnsNoData()
    {
        var result = await Fetch("""{"current":{"time":"2026-11-15T15:00"}}""", Query(true, false));

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_BadRequest_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("error-400.json"), HttpStatusCode.BadRequest);

        var result = await Fetch(handler, Query(true, false));

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_ServerError_ReturnsUnavailable()
    {
        var handler = FakeHttpHandler.Returning("{}", HttpStatusCode.BadGateway);

        var result = await Fetch(handler, Query(true, false));

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FetchAsync_MalformedJson_ReturnsUnavailable()
    {
        var result = await Fetch("not json", Query(true, false));

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    private static LocationQuery Query(bool includeCurrent, bool includeHourly) =>
        new(Location, 2, includeCurrent, includeHourly ? Window : null);

    private static Task<SourceResult<TestResult>> Fetch(string json, LocationQuery query) =>
        Fetch(FakeHttpHandler.Returning(json), query);

    private static Task<SourceResult<TestResult>> Fetch(FakeHttpHandler handler, LocationQuery query) =>
        new OpenMeteoLocationClient(handler.CreateClient("https://example.test/"), NullLogger.Instance).FetchAsync(
            Endpoint,
            query,
            values => new TestCurrent(values["field_a"], values["field_b"]),
            (current, hourly) => new TestResult(current, hourly),
            Ct);

    private static string Json(string? current, int hours, string? units = null)
    {
        var times = string.Join(',', Enumerable.Range(0, hours).Select(hour => $"\"{OpenMeteoHttp.FormatHour(Window.Start.AddHours(hour))}\""));
        var values = string.Join(',', Enumerable.Range(0, hours).Select(hour => hour.ToString(CultureInfo.InvariantCulture)));
        var sections = new List<string> { $$"""
            "hourly":{"time":[{{times}}],"field_a":[{{values}}],"field_b":[{{values}}]}
            """ };
        if (current is not null)
            sections.Add($"\"current\":{current}");
        if (units is not null)
            sections.Add($"\"hourly_units\":{units}");

        return "{" + string.Join(',', sections) + "}";
    }

    private sealed record TestCurrent(double A, double B);

    private sealed record TestResult(TestCurrent? Current, IReadOnlyList<HourlyReading> Hourly);
}
