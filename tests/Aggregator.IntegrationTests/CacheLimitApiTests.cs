using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aggregator.IntegrationTests;

[Requirement("R9")]
public sealed class CacheLimitApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnknownCity_IsCheckedAgainAfterTheNotFoundLifetime()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.WithSettings(("HttpClients:Geocoding:NoDataCacheTtl", "00:00:01")).CreateAuthorizedClientAsync();
        var unknown = new Uri("/city-report/GR/Xyzqwerty", UriKind.Relative);

        var first = await client.GetAsync(unknown, Ct);
        await client.GetAsync(unknown, Ct);
        var callsWhileCached = factory.OpenMeteo.CallsTo(OpenMeteoHosts.Geocoding);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), Ct);
        await client.GetAsync(unknown, Ct);

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(1, callsWhileCached);
        Assert.Equal(2, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Geocoding));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task FailingApi_IsNotCalledAgainDuringTheCooldown()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 1;
        factory.OpenMeteo.SetDown(OpenMeteoHosts.Weather, true);
        var client = await factory.CreateAuthorizedClientAsync();

        var first = (await client.GetJsonAsync("/city-report/GR/Anywhere")).GetProperty("places")[0];
        var second = (await client.GetJsonAsync("/city-report/GR/Anywhere")).GetProperty("places")[0];

        Assert.Equal(("unavailable", "unavailable"), (first.SourceStatus("weather"), second.SourceStatus("weather")));
        Assert.Equal(AggregatorApiFactory.AttemptsPerCall, factory.OpenMeteo.CallsTo(OpenMeteoHosts.Weather));
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData("5", 5)]
    public async Task InMemoryCacheSize_IsLimitedFromConfiguration(string? megabytes, long expectedMegabytes)
    {
        await using var factory = new AggregatorApiFactory();
        var app = megabytes is null ? factory : factory.WithSettings(("Cache:SizeLimitMegabytes", megabytes));

        var memory = app.Services.GetRequiredService<IOptions<MemoryCacheOptions>>().Value;

        Assert.Equal(expectedMegabytes * 1024 * 1024, memory.SizeLimit);
    }
}
