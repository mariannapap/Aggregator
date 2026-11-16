using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R2"), Requirement("R4")]
public sealed class HazardsApiTests
{
    private const string Report = "/city-report/GR/Anywhere";

    [Fact]
    public async Task IncludeHazards_ReturnsNearbyEventsFromTheSecondVendor_NearestFirst()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync($"{Report}?include=weather,hazards")).GetProperty("places")[0];

        var hazard = Assert.Single(place.GetProperty("hazards").EnumerateArray());
        Assert.Equal("Wildfire near the place", hazard.GetProperty("title").GetString());
        Assert.Equal("Wildfires", hazard.GetProperty("category").GetString());
        Assert.InRange(hazard.GetProperty("distanceKm").GetDouble(), 0, 1);
        Assert.Equal("ok", place.SourceStatus("hazards"));
        Assert.Equal("ok", place.SourceStatus("weather"));
    }

    [Fact]
    public async Task ByDefault_HazardsAreNotCalled()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        var client = await factory.CreateAuthorizedClientAsync();

        var place = (await client.GetJsonAsync(Report)).GetProperty("places")[0];

        Assert.False(place.TryGetProperty("hazards", out _));
        Assert.False(place.GetProperty("sources").TryGetProperty("hazards", out _));
        Assert.Equal(0, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Hazards));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task HazardsDown_OnlyHazardsAreUnavailable()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Hazards, true);
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync($"{Report}?include=weather,hazards");

        var place = body.GetProperty("places")[0];
        Assert.Equal("unavailable", place.SourceStatus("hazards"));
        Assert.Equal("ok", place.SourceStatus("weather"));
        Assert.True(body.GetProperty("degraded").GetBoolean());
    }

    [Fact]
    [Requirement("R11")]
    public async Task HazardsCalls_AreCountedInTheStatistics()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 2;
        var client = await factory.CreateAuthorizedClientAsync();

        await client.GetJsonAsync($"{Report}?include=hazards");
        var stats = await client.GetJsonAsync("/stats");

        Assert.Equal(2, stats.GetProperty("apis").GetProperty("hazards").GetProperty("totalRequests").GetInt64());
    }
}
