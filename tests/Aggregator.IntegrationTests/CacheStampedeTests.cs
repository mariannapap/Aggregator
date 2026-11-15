using System.Net;

namespace Aggregator.IntegrationTests;

[Requirement("R9")]
public sealed class CacheStampedeTests
{
    private const int Concurrent = 20;
    private const string Report = "/city-report/GR/Anywhere";
    private static readonly string[] AllApis = [OpenMeteoHosts.Geocoding, OpenMeteoHosts.Weather, OpenMeteoHosts.AirQuality, OpenMeteoHosts.Marine];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SimultaneousIdenticalRequests_CallEachApiOnce()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        foreach (var host in AllApis)
            factory.OpenMeteo.SetDelay(host, TimeSpan.FromMilliseconds(300));
        var client = await factory.CreateAuthorizedClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, Concurrent)
            .Select(_ => client.GetAsync(new Uri(Report, UriKind.Relative), Ct)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.All(AllApis, host => Assert.Equal(1, factory.OpenMeteo.CallsTo(host)));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task SimultaneousIdenticalRequestsWhileAnApiIsDown_DoNotFloodIt()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        factory.OpenMeteo.SetDelay(OpenMeteoHosts.Weather, TimeSpan.FromMilliseconds(300));
        var client = await factory.CreateAuthorizedClientAsync();

        var places = await Task.WhenAll(Enumerable.Range(0, Concurrent)
            .Select(async _ => (await client.GetJsonAsync(Report)).GetProperty("places")[0]));

        Assert.All(places, place => Assert.Equal("unavailable", place.GetProperty("sources").GetProperty("weather").GetString()));
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }
}
