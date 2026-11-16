using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R6")]
public sealed class DataAgeApiTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private static readonly string[] AllSources = ["geocoding", "weather", "airQuality", "marine"];

    [Fact]
    public async Task FreshReport_HasAFetchTimePerSource_AndIsNotDegraded()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();
        var before = DateTimeOffset.UtcNow;

        var body = await client.GetJsonAsync(Report);

        var place = body.GetProperty("places")[0];
        Assert.False(body.GetProperty("degraded").GetBoolean());
        Assert.All(AllSources, source => Assert.InRange(FetchedAt(place, source)!.Value, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1)));
    }

    [Fact]
    [FailureScenario]
    public async Task StaleSource_ShowsWhenItsDataWasFetched_AndTheReportIsDegraded()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Weather:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        var fresh = await client.GetJsonAsync(Report);
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);
        var stale = await client.GetJsonAsync(Report);

        var stalePlace = stale.GetProperty("places")[0];
        Assert.Equal("stale", stalePlace.SourceStatus("weather"));
        Assert.Equal(FetchedAt(fresh.GetProperty("places")[0], "weather"), FetchedAt(stalePlace, "weather"));
        Assert.True(stale.GetProperty("degraded").GetBoolean());
    }

    [Fact]
    [FailureScenario]
    public async Task UnavailableSource_HasNoFetchTime_AndTheReportIsDegraded()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.MarineDown = true;
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync(Report);

        Assert.Null(FetchedAt(body.GetProperty("places")[0], "marine"));
        Assert.True(body.GetProperty("degraded").GetBoolean());
    }

    [Fact]
    public async Task NoDataIsAnAnswer_NotADegradation()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.ScriptJson(OpenMeteoHosts.Marine, "{}");
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync(Report);

        Assert.Equal("no_data", body.GetProperty("places")[0].SourceStatus("marine"));
        Assert.False(body.GetProperty("degraded").GetBoolean());
    }

    private static DateTimeOffset? FetchedAt(JsonElement place, string source) =>
        place.GetProperty("sources").GetProperty(source).TryGetProperty("fetchedAt", out var fetchedAt) ? fetchedAt.GetDateTimeOffset() : null;
}
