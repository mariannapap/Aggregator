using System.Globalization;
using System.Net;

namespace Aggregator.IntegrationTests;

[Requirement("R6")]
public sealed class SourceStatusApiTests
{
    private const string Report = "/city-report/GR/Anywhere";

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
