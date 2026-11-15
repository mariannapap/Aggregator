using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R6")]
public sealed class ResilienceTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private const string WeatherResilience = "HttpClients:Weather:Resilience";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task TransientFailureThenSuccess_IsRetriedAndReportedOk()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.Script(OpenMeteoHosts.Weather, HttpStatusCode.ServiceUnavailable);
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("ok", Status(place, "weather"));
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }

    [Fact]
    [FailureScenario]
    public async Task TooManyRequestsWithRetryAfter_WaitsTheRequestedTimeThenRecovers()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.ScriptTooManyRequests(OpenMeteoHosts.Weather, TimeSpan.FromSeconds(1));
        var client = await factory.CreateAuthorizedClientAsync();

        var stopwatch = Stopwatch.StartNew();
        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("ok", Status(place, "weather"));
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900), $"Retry-After was not honoured ({stopwatch.Elapsed}).");
    }

    [Fact]
    [FailureScenario]
    public async Task ResponseSlowerThanAttemptTimeout_IsRetriedThenUnavailable()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.SetDelay(OpenMeteoHosts.Weather, TimeSpan.FromSeconds(3));
        var client = await factory.WithSettings(($"{WeatherResilience}:AttemptTimeout:Timeout", "00:00:00.200")).CreateAuthorizedClientAsync();

        var stopwatch = Stopwatch.StartNew();
        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("unavailable", Status(place, "weather"));
        Assert.Equal("ok", Status(place, "airQuality"));
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"The attempt timeout did not cut the slow call ({stopwatch.Elapsed}).");
    }

    [Fact]
    [FailureScenario]
    public async Task RepeatedFailures_OpenTheCircuit_LaterCallsFailFastWithoutReachingTheApi()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        var client = await factory.WithSettings(
            ($"{WeatherResilience}:CircuitBreaker:MinimumThroughput", "2"),
            ($"{WeatherResilience}:CircuitBreaker:FailureRatio", "0.5"),
            ($"{WeatherResilience}:CircuitBreaker:BreakDuration", "00:00:30")).CreateAuthorizedClientAsync();

        var first = (await client.GetJsonAsync(Report)).GetProperty("places")[0];
        var callsWhenOpened = factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather);
        var second = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("unavailable", Status(first, "weather"));
        Assert.Equal("unavailable", Status(second, "weather"));
        Assert.Equal(2, callsWhenOpened);
        Assert.Equal(callsWhenOpened, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }

    [Fact]
    [Requirement("R9"), FailureScenario]
    public async Task SourceDownAfterExpiry_ServesLastGoodValueAsStale()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Weather:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        var fresh = (await client.GetJsonAsync(Report)).GetProperty("places")[0];
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), Ct);
        var stale = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("ok", Status(fresh, "weather"));
        Assert.Equal("stale", Status(stale, "weather"));
        Assert.Equal(18.5, stale.GetProperty("weather").GetProperty("temperature").GetDouble());
    }

    [Fact]
    [Requirement("R9"), FailureScenario]
    public async Task GeocodingDownWithCachedCoordinates_Returns200WithGeocodingStale()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.WithSettings(("HttpClients:Geocoding:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        factory.OpenMeteo.GeocodingDown = true;
        await Task.Delay(TimeSpan.FromMilliseconds(1500), Ct);
        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.Equal("stale", Status(place, "geocoding"));
        Assert.Equal("ok", Status(place, "weather"));
    }

    [Fact]
    [Requirement("R10")]
    public async Task ConcurrencyLimit_NeverExceedsThePermitsPerApi()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 6;
        factory.OpenMeteo.SetDelay(OpenMeteoHosts.Weather, TimeSpan.FromMilliseconds(100));
        var client = await factory.WithSettings(
            ($"{WeatherResilience}:RateLimiter:DefaultRateLimiterOptions:PermitLimit", "2"),
            ($"{WeatherResilience}:RateLimiter:DefaultRateLimiterOptions:QueueLimit", "100")).CreateAuthorizedClientAsync();

        var places = (await client.GetJsonAsync(Report)).GetProperty("places").EnumerateArray().ToList();

        Assert.Equal(6, places.Count);
        Assert.All(places, place => Assert.Equal("ok", Status(place, "weather")));
        Assert.Equal(2, factory.OpenMeteo.MaxInFlight(OpenMeteoHosts.Weather));
    }

    [Fact]
    [Requirement("R10"), FailureScenario]
    public async Task ConcurrencyQueueFull_RejectedPlacesAreUnavailableButTheRequestSucceeds()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 4;
        factory.OpenMeteo.SetDelay(OpenMeteoHosts.Weather, TimeSpan.FromMilliseconds(300));
        var client = await factory.WithSettings(
            ($"{WeatherResilience}:RateLimiter:DefaultRateLimiterOptions:PermitLimit", "1"),
            ($"{WeatherResilience}:RateLimiter:DefaultRateLimiterOptions:QueueLimit", "0")).CreateAuthorizedClientAsync();

        var statuses = (await client.GetJsonAsync(Report)).GetProperty("places").EnumerateArray().Select(place => Status(place, "weather")).ToList();

        Assert.Contains("ok", statuses);
        Assert.Contains("unavailable", statuses);
        Assert.Equal(1, factory.OpenMeteo.MaxInFlight(OpenMeteoHosts.Weather));
    }

    private static string? Status(JsonElement place, string source) =>
        place.GetProperty("sources").GetProperty(source).GetString();
}
