using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.EndToEndTests;

public sealed class SmokeTests
{
    [Fact]
    public async Task ShippedImage_ServesTheWholeFlow()
    {
        using var anonymous = new HttpClient { BaseAddress = E2EEnvironment.ApiUrl };
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);

        using var session = await ApiSession.LoginAsync();
        var place = await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Smoke"));

        foreach (var source in new[] { "geocoding", "weather", "airQuality", "marine" })
            Assert.Equal("ok", place.SourceStatus(source));
        Assert.Equal(18.5, place.GetProperty("weather").GetProperty("temperature").GetDouble());
        Assert.Equal(54, place.GetProperty("airQuality").GetProperty("usAqi").GetInt32());
        Assert.Equal(0.3, place.GetProperty("marine").GetProperty("waveHeight").GetDouble());

        var notFound = await session.GetAsync("/city-report/GR/Nowhere");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        var withHazards = await (await session.GetAsync($"/city-report/GR/{E2EEnvironment.UniqueCity("Hazards")}?include=hazards"))
            .Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("no_data", withHazards.GetProperty("places")[0].SourceStatus("hazards"));

        var stats = await (await session.GetAsync("/stats")).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        foreach (var api in new[] { "geocoding", "weather", "airQuality", "marine", "hazards" })
            Assert.True(stats.GetProperty("apis").GetProperty(api).GetProperty("totalRequests").GetInt64() > 0);
    }
}
