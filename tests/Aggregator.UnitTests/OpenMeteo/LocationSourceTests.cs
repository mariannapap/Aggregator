using System.Net;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aggregator.UnitTests.OpenMeteo;

[Requirement("R2")]
public sealed class LocationSourceTests
{
    private static readonly HourlyWindow Window = HourlyWindow.StartingAtCurrentHour(Fixtures.RecordedAt, 24);
    private static readonly Coordinates Athens = new(37.98376, 23.72784);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Weather_CurrentAndHourly_ParsesEverything()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("weather-athens.json"));

        var result = await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, true, Window), Ct);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.NotNull(result.Value!.Current);
        Assert.Equal(48, result.Value.Hourly.Count);
        Assert.All(result.Value.Hourly, reading => Assert.True(Window.Contains(reading.Time)));
        Assert.Contains(result.Value.Hourly, reading => reading is { Metric: "temperature", Unit: "°C", Category: HourlyCategory.Weather });
        Assert.Contains(result.Value.Hourly, reading => reading is { Metric: "windSpeed", Unit: "km/h" });
    }

    [Fact]
    public async Task Weather_SendsRoundedCoordinatesAndAbsoluteHours()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("weather-athens.json"));

        await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, true, Window), Ct);

        Assert.Equal(
            "/v1/forecast?latitude=37.98&longitude=23.73&timezone=GMT&current=temperature_2m,wind_speed_10m&hourly=temperature_2m,wind_speed_10m&start_hour=2026-11-15T15:00&end_hour=2026-11-16T14:00",
            Uri.UnescapeDataString(Assert.Single(handler.Requests).PathAndQuery));
    }

    [Fact]
    public async Task Weather_CurrentOnly_DoesNotRequestHourly()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("weather-athens.json"));

        var result = await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, true, null), Ct);

        Assert.DoesNotContain("hourly=", Assert.Single(handler.Requests).Query, StringComparison.Ordinal);
        Assert.Empty(result.Value!.Hourly);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task Weather_MissingHourlyRows_ReturnsPartial()
    {
        var times = string.Join(',', Enumerable.Range(0, 23).Select(hour => $"\"{OpenMeteoHttp.FormatHour(Window.Start.AddHours(hour))}\""));
        var values = string.Join(',', Enumerable.Range(0, 23).Select(hour => hour == 5 ? "null" : "12.5"));
        var handler = FakeHttpHandler.Returning(
            $$$"""{"hourly":{"time":[{{{times}}}],"temperature_2m":[{{{values}}}],"wind_speed_10m":[{{{values}}}]}}""");

        var result = await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, false, Window), Ct);

        Assert.Equal(SourceStatus.Partial, result.Status);
        Assert.Equal(44, result.Value!.Hourly.Count);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task Weather_BadRequest_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("error-400.json"), HttpStatusCode.BadRequest);

        var result = await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, true, Window), Ct);

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task Weather_ServerError_ReturnsUnavailable()
    {
        var handler = FakeHttpHandler.Returning("{}", HttpStatusCode.ServiceUnavailable);

        var result = await Weather(handler).FetchAsync(new LocationQuery(Athens, 2, true, null), Ct);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task AirQuality_CurrentAndHourly_ParsesUsAqi()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("airquality-athens.json"));

        var result = await AirQuality(handler).FetchAsync(new LocationQuery(Athens, 2, true, Window), Ct);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.NotNull(result.Value!.Current);
        Assert.Equal(24, result.Value.Hourly.Count);
        Assert.All(result.Value.Hourly, reading => Assert.Equal(("usAqi", "USAQI", HourlyCategory.AirQuality), (reading.Metric, reading.Unit, reading.Category)));
        Assert.StartsWith("/v1/air-quality?latitude=37.98&longitude=23.73", Assert.Single(handler.Requests).PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task AirQuality_NoUsableValues_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning("""{"current":{"time":"2026-11-15T15:00"}}""");

        var result = await AirQuality(handler).FetchAsync(new LocationQuery(Athens, 2, true, null), Ct);

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    [Fact]
    public async Task Marine_Coast_ParsesWavesAndSeaTemperature()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("marine-piraeus.json"));

        var result = await Marine(handler).FetchAsync(new LocationQuery(new Coordinates(37.94, 23.64), 2, true, Window), Ct);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.NotNull(result.Value!.Current);
        Assert.Equal(48, result.Value.Hourly.Count);
        Assert.Contains(result.Value.Hourly, reading => reading is { Metric: "waveHeight", Unit: "m", Category: HourlyCategory.Marine });
        Assert.Contains(result.Value.Hourly, reading => reading is { Metric: "seaSurfaceTemperature", Unit: "°C" });
        Assert.StartsWith("/v1/marine?latitude=37.94&longitude=23.64", Assert.Single(handler.Requests).PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task Marine_Inland_ReturnsNoData()
    {
        var handler = FakeHttpHandler.Returning(Fixtures.Read("marine-inland.json"));

        var result = await Marine(handler).FetchAsync(new LocationQuery(new Coordinates(39.66, 20.85), 2, true, Window), Ct);

        Assert.Equal(SourceStatus.NoData, result.Status);
    }

    private static MarineSource Marine(FakeHttpHandler handler) =>
        new(handler.CreateClient("https://marine-api.open-meteo.com/"), NullLogger<MarineSource>.Instance);

    private static WeatherSource Weather(FakeHttpHandler handler) =>
        new(handler.CreateClient("https://api.open-meteo.com/"), NullLogger<WeatherSource>.Instance);

    private static AirQualitySource AirQuality(FakeHttpHandler handler) =>
        new(handler.CreateClient("https://air-quality-api.open-meteo.com/"), NullLogger<AirQualitySource>.Instance);
}
