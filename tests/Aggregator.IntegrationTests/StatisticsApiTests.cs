using System.Net;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R11")]
public sealed class StatisticsApiTests
{
    private const string Report = "/city-report/GR/Anywhere";
    private static readonly string[] AllApis = ["airQuality", "geocoding", "marine", "weather"];

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

    private static long Count(JsonElement stats, string api, string counter) =>
        stats.GetProperty("apis").GetProperty(api).GetProperty(counter).GetInt64();
}
