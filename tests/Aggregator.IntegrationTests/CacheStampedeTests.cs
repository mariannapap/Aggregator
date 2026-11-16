using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

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

        var responses = await SendWhileWeatherIsHeldAsync(factory, client => client.GetAsync(new Uri(Report, UriKind.Relative), Ct));

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

        var places = await SendWhileWeatherIsHeldAsync(factory, async client => (await client.GetJsonAsync(Report)).GetProperty("places")[0]);

        Assert.All(places, place => Assert.Equal("unavailable", place.SourceStatus("weather")));
        Assert.Equal(AggregatorApiFactory.AttemptsPerCall, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }

    // The weather API answers only after every request has reached the cache, so all of them share one fetch.
    private static async Task<T[]> SendWhileWeatherIsHeldAsync<T>(AggregatorApiFactory factory, Func<HttpClient, Task<T>> send)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.OpenMeteo.Hold(OpenMeteoHosts.Weather, release.Task);
        CountingHybridCache? cache = null;
        var client = await factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Decorate<HybridCache>((inner, _) => cache = new CountingHybridCache(inner, "weather|"))))
            .CreateAuthorizedClientAsync();

        var requests = Enumerable.Range(0, Concurrent).Select(_ => send(client)).ToList();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((cache?.Entered ?? 0) < Concurrent && DateTime.UtcNow < deadline)
            await Task.Delay(10, Ct);
        Assert.Equal(Concurrent, cache?.Entered);

        release.SetResult();
        return await Task.WhenAll(requests);
    }
}
