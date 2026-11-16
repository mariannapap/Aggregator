using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.IntegrationTests;

[Requirement("R11")]
public sealed class StatisticsApiTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private static readonly string[] AllApis = ["airQuality", "geocoding", "hazards", "marine", "weather"];

    [Fact]
    public async Task BeforeAnyCall_EveryApiIsListedWithNullAverages()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var apis = (await client.GetJsonAsync("/stats")).GetProperty("apis");

        Assert.Equal(AllApis, apis.EnumerateObject().Select(api => api.Name).Order(StringComparer.Ordinal));
        Assert.All(apis.EnumerateObject(), api =>
        {
            Assert.Equal(0, api.Value.GetProperty("totalRequests").GetInt64());
            Assert.Equal(0, api.Value.GetProperty("failedRequests").GetInt64());
            Assert.Equal(JsonValueKind.Null, api.Value.GetProperty("averageMs").ValueKind);
            Assert.All(
                api.Value.GetProperty("buckets").EnumerateObject(),
                bucket => Assert.Equal(JsonValueKind.Null, bucket.Value.GetProperty("averageMs").ValueKind));
        });
    }

    [Fact]
    public async Task Retries_AreCountedAsSeparateAttempts()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.Script(OpenMeteoHosts.Weather, HttpStatusCode.ServiceUnavailable);
        var client = await factory.CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        var stats = await client.GetJsonAsync("/stats");

        Assert.Equal(AggregatorApiFactory.AttemptsPerCall, Count(stats, "weather", "totalRequests"));
        Assert.Equal(1, Count(stats, "airQuality", "totalRequests"));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task ServerErrors_CountAsFailed_ButAnUpstream400DoesNot()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.Script(OpenMeteoHosts.Weather, HttpStatusCode.ServiceUnavailable);
        factory.OpenMeteo.Script(OpenMeteoHosts.Marine, HttpStatusCode.BadRequest);
        var client = await factory.CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        var stats = await client.GetJsonAsync("/stats");

        Assert.Equal(1, Count(stats, "weather", "failedRequests"));
        Assert.Equal((1L, 0L), (Count(stats, "marine", "totalRequests"), Count(stats, "marine", "failedRequests")));
        Assert.Equal(0, Count(stats, "airQuality", "failedRequests"));
    }

    [Fact]
    public async Task CacheHits_AreNotCounted()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 2;
        var client = await factory.CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        var afterFirst = await client.GetJsonAsync("/stats");
        await client.GetJsonAsync(Report);
        var afterSecond = await client.GetJsonAsync("/stats");

        Assert.All(AllApis, api => Assert.Equal(Count(afterFirst, api, "totalRequests"), Count(afterSecond, api, "totalRequests")));
        Assert.Equal(2, Count(afterSecond, "weather", "totalRequests"));
    }

    [Fact]
    [Requirement("O2")]
    public async Task AnomalyState_IsListedPerApi_AndUsesOnlyCompleteMinutes()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var client = await factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Anomaly:WindowMinutes", "1");
                builder.UseSetting("Anomaly:MinSamples", "3");
                builder.UseSetting("Statistics:RecentMinutesKept", "3");
                builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time));
            })
            .CreateAuthorizedClientAsync();

        var beforeAnyCall = await client.GetJsonAsync("/stats");
        await GeocodeUniqueCitiesAsync(client, 3);
        time.Advance(TimeSpan.FromMinutes(1));
        await GeocodeUniqueCitiesAsync(client, 3);
        var whileTheMinuteRuns = Anomaly(await client.GetJsonAsync("/stats"), "geocoding");
        time.Advance(TimeSpan.FromMinutes(1));
        var stats = await client.GetJsonAsync("/stats");
        var geocoding = Anomaly(stats, "geocoding");

        Assert.All(AllApis, api => Assert.Equal(
            """{"state":"insufficient_data","recentRequests":0,"recentAverageMs":null,"baselineRequests":0,"baselineAverageMs":null}""",
            Anomaly(beforeAnyCall, api).GetRawText()));
        // Minute M is the complete window and nothing came before it; the three calls of the minute in progress are in neither.
        Assert.Equal(("insufficient_data", 3, 0), (whileTheMinuteRuns.GetProperty("state").GetString(), whileTheMinuteRuns.GetProperty("recentRequests").GetInt64(), whileTheMinuteRuns.GetProperty("baselineRequests").GetInt64()));
        Assert.Equal(("normal", 3, 3), (geocoding.GetProperty("state").GetString(), geocoding.GetProperty("recentRequests").GetInt64(), geocoding.GetProperty("baselineRequests").GetInt64()));
        Assert.Equal(1, stats.GetProperty("anomalyRules").GetProperty("windowMinutes").GetInt32());
    }

    private static JsonElement Anomaly(JsonElement stats, string api) => stats.GetProperty("apis").GetProperty(api).GetProperty("anomaly");

    private static async Task GeocodeUniqueCitiesAsync(HttpClient client, int count)
    {
        for (var city = 0; city < count; city++)
            await client.GetJsonAsync($"/city-report/GR/Anomaly{Guid.NewGuid():N}?include=weather");
    }

    private static long Count(JsonElement stats, string api, string counter) =>
        stats.GetProperty("apis").GetProperty(api).GetProperty(counter).GetInt64();
}
