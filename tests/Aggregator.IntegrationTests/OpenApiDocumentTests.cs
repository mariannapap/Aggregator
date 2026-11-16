using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

[Requirement("R8")]
public sealed class OpenApiDocumentTests(OpenApiDocumentFixture fixture) : IClassFixture<OpenApiDocumentFixture>
{
    private const string CityReportPath = "/city-report/{country}/{city}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Requirement("O1")]
    public void Document_DeclaresTheBearerSchemeWithoutAGlobalRequirement()
    {
        Assert.Equal("bearer", BearerScheme(fixture.Document).GetProperty("scheme").GetString());
        Assert.False(fixture.Document.TryGetProperty("security", out _));
    }

    [Fact]
    [Requirement("O1")]
    public void BearerScheme_WithTheMockEnabled_PointsToMockLogin() =>
        Assert.Contains("/mock-login", BearerScheme(fixture.Document).GetProperty("description").GetString(), StringComparison.Ordinal);

    [Fact]
    [Requirement("O1")]
    public async Task BearerScheme_WithTheMockDisabled_DoesNotMentionMockLogin()
    {
        await using var factory = new AggregatorApiFactory();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MockAuth:Enabled", "false");
            builder.UseSetting("Auth:Authority", "https://login.example.com/tenant/v2.0");
            builder.UseSetting("OpenApi:EnableUi", "true");
        }).CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        Assert.DoesNotContain("mock-login", BearerScheme(document).GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CityReportPath, "get")]
    [InlineData("/stats", "get")]
    [Requirement("O1")]
    public void ProtectedOperations_RequireBearerAndDocument401And403(string path, string method)
    {
        var operation = Operation(path, method);

        Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("403", out _));
    }

    [Fact]
    [Requirement("O1")]
    public void MockLogin_IsAnonymousInTheDocument() =>
        Assert.False(Operation("/mock-login", "post").TryGetProperty("security", out _));

    [Theory]
    [InlineData("sort", "relevance")]
    [InlineData("region", "Fuzzy")]
    [InlineData("hourly.category", "airQuality")]
    [InlineData("include", "marine")]
    [InlineData("country", "ISO 3166-1")]
    public void CityReportParameters_HaveDescriptions(string name, string expectedText)
    {
        var parameter = Operation(CityReportPath, "get").GetProperty("parameters").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);

        Assert.Contains(expectedText, parameter.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CityReportSummary_NamesEveryLocationApi() =>
        Assert.All(
            ["weather", "air quality", "marine"],
            api => Assert.Contains(api, Operation(CityReportPath, "get").GetProperty("summary").GetString(), StringComparison.Ordinal));

    [Fact]
    public void SourceStatus_IsDocumentedAsAnEnumWithEveryValue()
    {
        var status = fixture.Document.GetProperty("components").GetProperty("schemas").GetProperty("SourceStatusResponse");

        Assert.Equal(
            ["no_data", "ok", "partial", "stale", "unavailable"],
            status.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("WeatherResponse", "temperature", "°C")]
    [InlineData("WeatherResponse", "windSpeed", "km/h")]
    [InlineData("AirQualityResponse", "usAqi", "0–500")]
    [InlineData("MarineResponse", "waveHeight", "metres")]
    [InlineData("SourceResponse", "fetchedAt", "stale")]
    [InlineData("CityReportResponse", "degraded", "no_data does not count")]
    [InlineData("FiltersResponse", "truncated", "limit or offset")]
    [InlineData("ApiStatisticsResponse", "totalRequests", "HTTP attempts")]
    [InlineData("AnomalyResponse", "state", "insufficient_data")]
    public void ContractFields_HaveDescriptionsFromTheXmlComments(string schema, string property, string expectedText)
    {
        var description = fixture.Document.GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").GetProperty(property).GetProperty("description").GetString();

        Assert.Contains(expectedText, description, StringComparison.Ordinal);
    }

    private static JsonElement BearerScheme(JsonElement document) =>
        document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");

    private JsonElement Operation(string path, string method) =>
        fixture.Document.GetProperty("paths").GetProperty(path).GetProperty(method);
}
