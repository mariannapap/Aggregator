using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

public sealed class CityReportApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CityReport_WithoutToken_Returns401()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CityReport_TokenSignedWithUntrustedKey_Returns401()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "invalid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CityReport_TokenWithoutRole_Returns403()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "forbidden");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CityReport_ValidRequest_AggregatesAllSourcesPerPlace()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "valid");

        var body = await GetJson(client, "/city-report/GR/Athens?include=weather,airQuality,hourly");

        var places = body.GetProperty("places");
        Assert.Equal(2, places.GetArrayLength());
        var athens = places[0];
        Assert.Equal("Athens", athens.GetProperty("city").GetString());
        Assert.True(athens.GetProperty("weather").GetProperty("temperature").ValueKind == JsonValueKind.Number);
        Assert.True(athens.GetProperty("airQuality").GetProperty("usAqi").ValueKind == JsonValueKind.Number);
        Assert.Equal(72, athens.GetProperty("hourly").GetArrayLength());
        Assert.Equal("ok", athens.GetProperty("sources").GetProperty("geocoding").GetString());
        Assert.Equal("ok", athens.GetProperty("sources").GetProperty("weather").GetString());
        Assert.Equal("ok", athens.GetProperty("sources").GetProperty("airQuality").GetString());
    }

    [Fact]
    public async Task CityReport_OneSourceDown_Returns200WithItsStatus()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.AirQualityDown = true;
        var client = await AuthorizedClient(factory, "valid");

        var body = await GetJson(client, "/city-report/GR/Athens");

        var athens = body.GetProperty("places")[0];
        Assert.Equal("unavailable", athens.GetProperty("sources").GetProperty("airQuality").GetString());
        Assert.Equal("ok", athens.GetProperty("sources").GetProperty("weather").GetString());
        Assert.False(athens.TryGetProperty("airQuality", out _));
    }

    [Fact]
    public async Task CityReport_SecondRequest_IsServedFromCache()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "valid");

        await GetJson(client, "/city-report/GR/Athens");
        var callsAfterFirst = factory.OpenMeteo.Calls;
        await GetJson(client, "/city-report/GR/Athens");

        Assert.Equal(callsAfterFirst, factory.OpenMeteo.Calls);
    }

    [Fact]
    public async Task CityReport_UnknownCity_Returns404ProblemDetails()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "valid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Xyzqwerty", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CityReport_GeocodingDownWithoutCache_Returns503ProblemDetails()
    {
        await using var factory = new AggregatorApiFactory();
        factory.OpenMeteo.GeocodingDown = true;
        var client = await AuthorizedClient(factory, "valid");

        var response = await client.GetAsync(new Uri("/city-report/GR/Athens", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("unavailable", problem.GetProperty("sources").GetProperty("geocoding").GetString());
    }

    [Fact]
    public async Task CityReport_InvalidQuery_Returns400ValidationProblem()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "valid");

        var response = await client.GetAsync(new Uri("/city-report/GRC/Athens?sort=temperature&include=airQuality", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("country", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("sort", out _));
    }

    [Fact]
    public async Task Statistics_AfterCalls_ReportsEveryApi()
    {
        await using var factory = new AggregatorApiFactory();
        var client = await AuthorizedClient(factory, "valid");
        await GetJson(client, "/city-report/GR/Athens");

        var stats = await GetJson(client, "/stats");

        var apis = stats.GetProperty("apis");
        Assert.Equal(1, apis.GetProperty("geocoding").GetProperty("totalRequests").GetInt64());
        Assert.Equal(2, apis.GetProperty("weather").GetProperty("totalRequests").GetInt64());
        Assert.Equal(2, apis.GetProperty("airQuality").GetProperty("totalRequests").GetInt64());
        Assert.Equal(100, stats.GetProperty("thresholds").GetProperty("fastBelowMs").GetInt32());
    }

    [Fact]
    public async Task MockLogin_UnknownUser_Returns400()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "0" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
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
    public async Task Health_IsAnonymous()
    {
        await using var factory = new AggregatorApiFactory();

        var response = await factory.CreateClient().GetAsync(new Uri("/health", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpClient> AuthorizedClient(AggregatorApiFactory factory, string user)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user }, Ct);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
