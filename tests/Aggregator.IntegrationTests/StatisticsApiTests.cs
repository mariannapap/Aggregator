using System.Net;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R11")]
public sealed class StatisticsApiTests
{
    private const string Report = "/city-report/GR/Anywhere";

    [Fact]
    public async Task Retries_AreCountedAsSeparateAttempts()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.Script(OpenMeteoHosts.Weather, HttpStatusCode.ServiceUnavailable);
        var client = await factory.CreateAuthorizedClientAsync();

        await client.GetJsonAsync(Report);
        var stats = await client.GetJsonAsync("/stats");

        Assert.Equal(2, TotalRequests(stats, "weather"));
        Assert.Equal(1, TotalRequests(stats, "airQuality"));
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

        foreach (var api in new[] { "geocoding", "weather", "airQuality", "marine" })
            Assert.Equal(TotalRequests(afterFirst, api), TotalRequests(afterSecond, api));
        Assert.Equal(2, TotalRequests(afterSecond, "weather"));
    }

    private static long TotalRequests(JsonElement stats, string api) =>
        stats.GetProperty("apis").GetProperty(api).GetProperty("totalRequests").GetInt64();
}
