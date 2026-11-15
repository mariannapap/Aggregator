using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

public sealed class CityReportApiTests
{
    private static readonly string[] LocationSources = ["weather", "airQuality", "marine"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Requirement("O1"), FailureScenario]
    public async Task CityReport_WithoutToken_Returns401()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("O1"), FailureScenario]
    public async Task CityReport_TokenSignedWithUntrustedKey_Returns401()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("invalid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("O1"), FailureScenario]
    public async Task CityReport_TokenWithoutRole_Returns403()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("forbidden");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Requirement("R3"), Requirement("R4")]
    public async Task CityReport_ValidRequest_AggregatesAllSourcesPerPlace()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var body = await client.GetJsonAsync("/city-report/GR/Athens?include=weather,airQuality,marine,hourly");

        var places = body.GetProperty("places");
        Assert.Equal(2, places.GetArrayLength());
        var athens = places[0];
        Assert.Equal("Athens", athens.GetProperty("city").GetString());
        Assert.True(athens.GetProperty("weather").GetProperty("temperature").ValueKind == JsonValueKind.Number);
        Assert.True(athens.GetProperty("airQuality").GetProperty("usAqi").ValueKind == JsonValueKind.Number);
        Assert.True(athens.GetProperty("marine").GetProperty("waveHeight").ValueKind == JsonValueKind.Number);
        Assert.Equal(24 * 5, athens.GetProperty("hourly").GetArrayLength());
        foreach (var source in LocationSources.Prepend("geocoding"))
            Assert.Equal("ok", athens.GetProperty("sources").GetProperty(source).GetString());
    }

    [Fact]
    [Requirement("R5")]
    public async Task CityReport_HourlyFollowsInclude_OnlyIncludedSectionsProduceRows()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var athens = (await client.GetJsonAsync("/city-report/GR/Athens?include=weather,airQuality,hourly")).GetProperty("places")[0];

        Assert.Equal(24 * 3, athens.GetProperty("hourly").GetArrayLength());
        Assert.DoesNotContain(athens.GetProperty("hourly").EnumerateArray(), row => row.GetProperty("category").GetString() == "marine");
        Assert.False(athens.GetProperty("sources").TryGetProperty("marine", out _));
    }

    [Fact]
    [Requirement("R3"), Requirement("R4")]
    public async Task CityReport_WithMarine_AggregatesThreeLocationApisPerPlace()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var body = await client.GetJsonAsync("/city-report/GR/Athens?include=weather,airQuality,marine,hourly&hourly.category=marine");

        var athens = body.GetProperty("places")[0];
        Assert.Equal(0.1, athens.GetProperty("marine").GetProperty("waveHeight").GetDouble());
        Assert.Equal(20.5, athens.GetProperty("marine").GetProperty("seaSurfaceTemperature").GetDouble());
        Assert.Equal("ok", athens.GetProperty("sources").GetProperty("marine").GetString());
        Assert.Equal(48, athens.GetProperty("hourly").GetArrayLength());
        Assert.All(athens.GetProperty("hourly").EnumerateArray(), row => Assert.Equal("marine", row.GetProperty("category").GetString()));
    }

    [Fact]
    [Requirement("R3"), Requirement("R4")]
    public async Task CityReport_DefaultInclude_CallsWeatherAirQualityAndMarine()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var athens = (await client.GetJsonAsync("/city-report/GR/Athens")).GetProperty("places")[0];

        Assert.True(athens.TryGetProperty("weather", out _));
        Assert.True(athens.TryGetProperty("airQuality", out _));
        Assert.True(athens.TryGetProperty("marine", out _));
        Assert.False(athens.TryGetProperty("hourly", out _));
    }

    [Fact]
    [Requirement("R5"), Requirement("R6"), FailureScenario]
    public async Task CityReport_HourlyCategoryNotIncluded_Returns400()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens?include=weather,hourly&hourly.category=marine", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("hourly.category", out _));
    }

    [Theory]
    [InlineData("airQuality")]
    [InlineData("marine")]
    [Requirement("R6"), FailureScenario]
    public async Task CityReport_OneSourceDown_Returns200WithItsStatusAndOthersIntact(string down)
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.AirQualityDown = down == "airQuality";
        factory.OpenMeteo.MarineDown = down == "marine";
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var body = await client.GetJsonAsync("/city-report/GR/Athens");

        var athens = body.GetProperty("places")[0];
        var sources = athens.GetProperty("sources");
        Assert.Equal("unavailable", sources.GetProperty(down).GetString());
        Assert.False(athens.TryGetProperty(down, out _));
        foreach (var source in LocationSources.Where(source => source != down))
        {
            Assert.Equal("ok", sources.GetProperty(source).GetString());
            Assert.True(athens.TryGetProperty(source, out _));
        }
    }

    [Fact]
    [Requirement("R9")]
    public async Task CityReport_SecondRequest_IsServedFromCache()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        await client.GetJsonAsync("/city-report/GR/Athens");
        var callsAfterFirst = factory.OpenMeteo.Calls;
        await client.GetJsonAsync("/city-report/GR/Athens");

        Assert.Equal(callsAfterFirst, factory.OpenMeteo.Calls);
    }

    [Fact]
    [Requirement("R4"), Requirement("R6"), FailureScenario]
    public async Task CityReport_UnknownCity_Returns404ProblemDetails()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Xyzqwerty", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Requirement("R4"), Requirement("R6"), FailureScenario]
    public async Task CityReport_GeocodingDownWithoutCache_Returns503ProblemDetails()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeocodingDown = true;
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("unavailable", problem.GetProperty("sources").GetProperty("geocoding").GetString());
    }

    [Fact]
    [Requirement("R5"), Requirement("R6"), FailureScenario]
    public async Task CityReport_InvalidQuery_Returns400ValidationProblem()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");

        var response = await client.GetAsync(new Uri("/city-report/GRC/Athens?sort=temperature&include=airQuality", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("country", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("sort", out _));
    }

    [Fact]
    [Requirement("R11")]
    public async Task Statistics_AfterCalls_ReportsEveryApi()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync("valid");
        await client.GetJsonAsync("/city-report/GR/Athens");

        var stats = await client.GetJsonAsync("/stats");

        var apis = stats.GetProperty("apis");
        Assert.Equal(1, apis.GetProperty("geocoding").GetProperty("totalRequests").GetInt64());
        Assert.Equal(2, apis.GetProperty("weather").GetProperty("totalRequests").GetInt64());
        Assert.Equal(2, apis.GetProperty("airQuality").GetProperty("totalRequests").GetInt64());
        Assert.Equal(2, apis.GetProperty("marine").GetProperty("totalRequests").GetInt64());
        Assert.Equal(100, stats.GetProperty("thresholds").GetProperty("fastBelowMs").GetInt32());
    }

    [Fact]
    [Requirement("O1"), FailureScenario]
    public async Task MockLogin_UnknownUser_Returns400()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "0" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Requirement("O1")]
    public async Task OptionalEndpoints_WhenDisabled_AreNotMapped()
    {
        await using var factory = new AggregatorApiFactory();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MockAuth:Enabled", "false");
            builder.UseSetting("Auth:Authority", "https://login.example.com/tenant/v2.0");
            builder.UseSetting("OpenApi:EnableUi", "false");
        }).CreateClient();

        var login = await client.PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "valid" }, Ct);
        var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, document.StatusCode);
    }

    [Fact]
    [Requirement("R4")]
    public async Task CityReport_LowerCaseCountry_IsAccepted()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await factory.CreateAuthorizedClientAsync();

        var athens = (await client.GetJsonAsync("/city-report/gr/Athens")).GetProperty("places")[0];

        Assert.Equal("GR", athens.GetProperty("country").GetString());
    }

    [Fact]
    [Requirement("R5")]
    public async Task CityReport_RegionSortAndHourlyCategory_AreAppliedEndToEnd()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeneratedPlaces = 4;
        var client = await factory.CreateAuthorizedClientAsync();

        var body = await client.GetJsonAsync("/city-report/GR/Anywhere?region=krete&sort=-temperature&include=weather,marine,hourly&hourly.category=marine");

        var places = body.GetProperty("places").EnumerateArray().ToList();
        Assert.Equal(2, places.Count);
        Assert.All(places, place => Assert.Equal("Crete", place.GetProperty("region").GetString()));
        Assert.All(places, place => Assert.All(
            place.GetProperty("hourly").EnumerateArray(),
            row => Assert.Equal("marine", row.GetProperty("category").GetString())));
        Assert.All(places, place => Assert.False(place.TryGetProperty("airQuality", out _)));
        var region = body.GetProperty("filters").GetProperty("region");
        Assert.True(region.GetProperty("applied").GetBoolean());
        Assert.Equal("Crete", region.GetProperty("matched")[0].GetString());
    }

    [Fact]
    [Requirement("O1")]
    public async Task Health_IsAnonymous()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().GetAsync(new Uri("/health", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
