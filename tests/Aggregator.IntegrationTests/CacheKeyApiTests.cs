using System.Net;

namespace Aggregator.IntegrationTests;

// The city name goes into the geocoding cache key, so a name must never reach the entries kept for another city.
[Requirement("R9")]
public sealed class CacheKeyApiTests
{
    private const string Athens = "/city-report/GR/Athens?include=weather";
    private const string ForgedLastGoodKey = "/city-report/GR/Athens%7Clast-good?include=weather";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CityNameEndingInACacheMarker_DoesNotReadAnotherCitysEntry()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var athens = await client.GetAsync(new Uri(Athens, UriKind.Relative), Ct);
        var forged = await client.GetAsync(new Uri(ForgedLastGoodKey, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, athens.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Geocoding));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task CityNameEndingInACacheMarker_DoesNotOverwriteAnotherCitysFallback()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.WithSettings(("HttpClients:Geocoding:CacheTtl", "00:00:01")).CreateAuthorizedClientAsync();

        await client.GetAsync(new Uri(Athens, UriKind.Relative), Ct);
        await client.GetAsync(new Uri(ForgedLastGoodKey, UriKind.Relative), Ct);
        factory.OpenMeteo.GeocodingDown = true;
        await Task.Delay(TimeSpan.FromMilliseconds(1500), Ct);
        var athens = await client.GetJsonAsync(Athens);

        Assert.Equal("stale", athens.GetProperty("places")[0].SourceStatus("geocoding"));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task CityNameEndingInACacheMarker_CannotPlantAFallbackForAnotherCity()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var planted = await client.GetAsync(new Uri(ForgedLastGoodKey, UriKind.Relative), Ct);
        factory.OpenMeteo.GeocodingDown = true;
        var athens = await client.GetAsync(new Uri(Athens, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, planted.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, athens.StatusCode);
    }
}
