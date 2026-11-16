using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R9")]
public sealed class CacheBehaviourTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private static readonly TimeSpan PastShortTtl = TimeSpan.FromMilliseconds(1500);
    private static readonly string[] AllApis = [OpenMeteoHosts.Geocoding, OpenMeteoHosts.Weather, OpenMeteoHosts.AirQuality, OpenMeteoHosts.Marine];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DifferentRequestShapes_DoNotShareAnEntry()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();

        var currentOnly = (await client.GetJsonAsync($"{Report}?include=weather")).GetProperty("places")[0];
        var withHourly = (await client.GetJsonAsync($"{Report}?include=weather,hourly")).GetProperty("places")[0];
        await client.GetJsonAsync($"{Report}?include=weather");

        Assert.False(currentOnly.TryGetProperty("hourly", out _));
        Assert.Equal(48, withHourly.GetProperty("hourly").GetArrayLength());
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task OneSourceFails_TheOthersKeepServingTheirOwnCachedEntries()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Weather:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        await Task.Delay(PastShortTtl, Ct);
        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("stale", place.SourceStatus("weather"));
        Assert.Equal("ok", place.SourceStatus("airQuality"));
        Assert.Equal("ok", place.SourceStatus("marine"));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.AirQuality));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Marine));
    }

    [Fact]
    public async Task DifferentFiltersAndSorts_ReuseTheSourceEntries_ButEachGetsItsOwnResponse()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 4;
        var client = await factory.CreateAuthorizedClientAsync();

        var all = await client.GetJsonAsync(Report);
        var callsAfterFirst = AllApis.Select(factory.OpenMeteo.CallsTo).ToList();
        var crete = await client.GetJsonAsync($"{Report}?region=krete&sort=-temperature");
        var byName = await client.GetJsonAsync($"{Report}?sort=name");

        Assert.Equal(callsAfterFirst, AllApis.Select(factory.OpenMeteo.CallsTo).ToList());
        Assert.Equal(4, all.GetProperty("places").GetArrayLength());
        Assert.Equal(2, crete.GetProperty("places").GetArrayLength());
        Assert.All(crete.GetProperty("places").EnumerateArray(), place => Assert.Equal("Crete", place.GetProperty("region").GetString()));
        Assert.Equal(
            Names(byName).Order(StringComparer.InvariantCultureIgnoreCase),
            Names(byName));
        Assert.NotEqual(Names(all), Names(byName));
    }

    [Fact]
    public async Task TtlsApplyPerSource_OnlyTheExpiredSourceIsRefetched()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Weather:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        await Task.Delay(PastShortTtl, Ct);
        await client.GetJsonAsync(Report);

        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.AirQuality));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Marine));
        Assert.Equal(1, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Geocoding));
    }

    private static List<string?> Names(JsonElement body) =>
        body.GetProperty("places").EnumerateArray().Select(place => place.GetProperty("city").GetString()).ToList();
}
