using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

public sealed class OpenApiDocumentTests
{
    private const string CityReportPath = "/city-report/{country}/{city}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Document_DeclaresBearerSchemeOnceAndNoGlobalRequirement()
    {
        var document = await GetDocumentAsync();

        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.False(document.TryGetProperty("security", out _));
    }

    [Theory]
    [InlineData(CityReportPath, "get")]
    [InlineData("/stats", "get")]
    public async Task ProtectedOperations_RequireBearerAndDocument401And403(string path, string method)
    {
        var operation = Operation(await GetDocumentAsync(), path, method);

        Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("403", out _));
    }

    [Fact]
    public async Task MockLogin_IsAnonymousInTheDocument()
    {
        var operation = Operation(await GetDocumentAsync(), "/mock-login", "post");

        Assert.False(operation.TryGetProperty("security", out _));
    }

    [Theory]
    [InlineData("sort", "relevance")]
    [InlineData("region", "Fuzzy")]
    [InlineData("hourly.category", "airQuality")]
    [InlineData("include", "marine")]
    [InlineData("country", "ISO 3166-1")]
    public async Task CityReportParameters_HaveDescriptions(string name, string expectedText)
    {
        var parameters = Operation(await GetDocumentAsync(), CityReportPath, "get").GetProperty("parameters").EnumerateArray();

        var parameter = parameters.Single(item => item.GetProperty("name").GetString() == name);
        Assert.Contains(expectedText, parameter.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    private static JsonElement Operation(JsonElement document, string path, string method) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static async Task<JsonElement> GetDocumentAsync()
    {
        await using var factory = new AggregatorApiFactory();
        using var client = factory.WithWebHostBuilder(builder => builder.UseSetting("OpenApi:EnableUi", "true")).CreateClient();
        return await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), Ct);
    }
}
