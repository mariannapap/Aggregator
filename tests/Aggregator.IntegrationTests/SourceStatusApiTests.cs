using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R6")]
public sealed class SourceStatusApiTests
{
    private const string Report = "/city-report/GR/Anywhere";

    // Valid JSON, but no hourly time is in Open-Meteo's yyyy-MM-ddTHH:mm format, so the answer counts as a failure.
    private const string MalformedWeather =
        """{"current":{"temperature_2m":1,"wind_speed_10m":2},"hourly":{"time":["2026-11-15 15:00"],"temperature_2m":[1],"wind_speed_10m":[2]}}""";

    [Fact]
    [FailureScenario]
    public async Task IncompleteCurrentValues_ArePartial_AndTheHourlyRowsAreKept()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.OmitCurrentField(OpenMeteoHosts.Weather, "wind_speed_10m");
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync($"{Report}?include=weather,hourly&hourly.category=weather")).GetProperty("places")[0];

        Assert.Equal("partial", place.SourceStatus("weather"));
        Assert.False(place.TryGetProperty("weather", out _));
        Assert.Equal(24 * 2, place.GetProperty("hourly").GetArrayLength());
    }

    [Fact]
    [FailureScenario]
    public async Task OneUnreadableHourlyTime_IsPartial_AndTheOtherRowsAreKept()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.ReplaceHourlyTime(OpenMeteoHosts.Weather, 5, "null");
        var client = await factory.CreateAuthorizedClientAsync();

        var response = await client.GetJsonAsync($"{Report}?include=weather,hourly&hourly.category=weather");
        var place = response.GetProperty("places")[0];

        Assert.Equal("partial", place.SourceStatus("weather"));
        Assert.True(place.TryGetProperty("weather", out _));
        Assert.Equal(23 * 2, place.GetProperty("hourly").GetArrayLength());
        Assert.True(response.GetProperty("degraded").GetBoolean());
    }

    [Fact]
    [FailureScenario]
    public async Task AnswerWithoutUsableValues_IsNoData_AndTheOtherSourcesAreUnaffected()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Marine, "{}");
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("no_data", place.SourceStatus("marine"));
        Assert.False(place.TryGetProperty("marine", out _));
        Assert.Equal("ok", place.SourceStatus("weather"));
    }

    [Fact]
    [FailureScenario]
    public async Task UpstreamBadRequest_IsCalledOnceWithoutRetry_AndReportedAsNoData()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.Script(OpenMeteoHosts.Marine, HttpStatusCode.BadRequest);
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("no_data", place.SourceStatus("marine"));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Marine));
    }

    [Fact]
    [FailureScenario]
    public async Task MalformedValueFromOneSource_DegradesOnlyThatSource()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Weather, MalformedWeather);
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync($"{Report}?include=weather,airQuality,hourly")).GetProperty("places")[0];

        Assert.Equal("unavailable", place.SourceStatus("weather"));
        Assert.Equal("ok", place.SourceStatus("airQuality"));
    }

    [Fact]
    [Requirement("R9"), FailureScenario]
    public async Task MalformedValueAfterExpiry_ServesTheLastGoodValueAsStale()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Weather:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();
        const string request = $"{Report}?include=weather,airQuality,hourly";

        var fresh = (await client.GetJsonAsync(request)).GetProperty("places")[0];
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Weather, MalformedWeather);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);
        var degraded = (await client.GetJsonAsync(request)).GetProperty("places")[0];

        Assert.Equal("ok", fresh.SourceStatus("weather"));
        Assert.Equal("stale", degraded.SourceStatus("weather"));
        Assert.Equal(fresh.GetProperty("weather").GetProperty("temperature").GetDouble(), degraded.GetProperty("weather").GetProperty("temperature").GetDouble());
        Assert.Equal("ok", degraded.SourceStatus("airQuality"));
    }

    [Theory]
    [InlineData(-7, -1)]
    [InlineData(3, 4)]
    [Requirement("R5"), FailureScenario]
    public async Task HourlyRangeOutsideTheWindow_Returns400NamingTheWindow_WithoutCallingAnyApi(int fromDays, int toDays)
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();
        var now = DateTimeOffset.UtcNow;
        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

        var response = await client.GetAsync(new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{Report}?include=weather,hourly&hourly.from={hour.AddDays(fromDays):yyyy-MM-dd'T'HH:mm}Z&hourly.to={hour.AddDays(toDays):yyyy-MM-dd'T'HH:mm}Z"), UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains("must overlap the hourly window", problem.GetProperty("errors").GetProperty("hourly")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal(0, factory.OpenMeteo.Calls);
    }

    [Fact]
    [Requirement("R5")]
    public async Task HourlyBounds_AreClampedAndReportedInFilters()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddDays(-2);
        var to = from.AddDays(2).AddHours(3);

        var body = await client.GetJsonAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{Report}?include=weather,hourly&hourly.category=weather&hourly.from={from:yyyy-MM-dd'T'HH:mm}Z&hourly.to={to:yyyy-MM-dd'T'HH:mm}Z"));

        var hourly = body.GetProperty("filters").GetProperty("hourly");
        var rows = body.GetProperty("places")[0].GetProperty("hourly").EnumerateArray().Select(row => row.GetProperty("time").GetDateTimeOffset()).ToList();
        var appliedFrom = hourly.GetProperty("appliedFrom").GetDateTimeOffset();
        Assert.Equal(from, hourly.GetProperty("requestedFrom").GetDateTimeOffset());
        Assert.Equal(to, hourly.GetProperty("requestedTo").GetDateTimeOffset());
        Assert.True(appliedFrom > from);
        Assert.Equal(to, hourly.GetProperty("appliedTo").GetDateTimeOffset());
        Assert.True(hourly.GetProperty("clamped").GetBoolean());
        Assert.Equal(appliedFrom, rows.Min());
        Assert.Equal(to, rows.Max());
    }
}
