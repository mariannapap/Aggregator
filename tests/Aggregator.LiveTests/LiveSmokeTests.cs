using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.LiveTests;

// Catches upstream changes that the stubs and WireMock mappings cannot: renamed fields, new formats, moved endpoints.
// Only shapes and plausible ranges are checked, never exact values.
public sealed class LiveSmokeTests(LiveApi api) : IClassFixture<LiveApi>
{
    private static readonly string[] AlwaysAnswering = ["geocoding", "weather", "airQuality"];
    private static readonly string[] AnsweringOrEmpty = ["ok", "no_data"];
    private static readonly string[] HourlyMetrics = ["temperature", "windSpeed", "usAqi"];
    private static readonly string[] AllApis = ["geocoding", "weather", "airQuality", "marine", "hazards"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Athens_EveryApiAnswers_InTheShapeTheAdaptersExpect()
    {
        Assert.SkipUnless(LiveApi.Enabled, LiveApi.SkipReason);
        var client = await api.CreateAuthorizedClientAsync(Ct);

        var report = await GetJsonAsync(client, "/city-report/GR/Athens?include=weather,airQuality,marine,hazards,hourly&limit=1");
        var place = report.GetProperty("places")[0];
        var hourly = place.GetProperty("hourly").EnumerateArray().Select(row => row.GetProperty("metric").GetString()).ToList();
        var stats = await GetJsonAsync(client, "/stats");

        Assert.Equal("Athens", place.GetProperty("city").GetString());
        Assert.All(AlwaysAnswering, source => Assert.Equal("ok", place.SourceStatus(source)));
        Assert.Contains(place.SourceStatus("marine"), AnsweringOrEmpty);
        Assert.Contains(place.SourceStatus("hazards"), AnsweringOrEmpty);
        Assert.InRange(place.GetProperty("weather").GetProperty("temperature").GetDouble(), -40, 55);
        Assert.InRange(place.GetProperty("airQuality").GetProperty("usAqi").GetInt32(), 0, 500);
        Assert.All(HourlyMetrics, metric => Assert.Equal(24, hourly.Count(name => name == metric)));
        Assert.All(AllApis, name =>
            Assert.True(stats.GetProperty("apis").GetProperty(name).GetProperty("totalRequests").GetInt64() >= 1, $"No call to {name} was counted."));
    }

    [Fact]
    public async Task Piraeus_TheMarineApiAnswersWithSeaConditions()
    {
        Assert.SkipUnless(LiveApi.Enabled, LiveApi.SkipReason);
        var client = await api.CreateAuthorizedClientAsync(Ct);

        var place = (await GetJsonAsync(client, "/city-report/GR/Piraeus?include=marine&limit=1")).GetProperty("places")[0];

        Assert.Equal("ok", place.SourceStatus("marine"));
        Assert.InRange(place.GetProperty("marine").GetProperty("waveHeight").GetDouble(), 0, 30);
        Assert.InRange(place.GetProperty("marine").GetProperty("seaSurfaceTemperature").GetDouble(), -2, 40);
    }

    [Fact]
    public async Task UnknownCity_TheGeocoderFindsNothing_And404IsReturned()
    {
        Assert.SkipUnless(LiveApi.Enabled, LiveApi.SkipReason);
        var client = await api.CreateAuthorizedClientAsync(Ct);

        var response = await client.GetAsync(new Uri("/city-report/GR/Xqzvwplkjh", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
