using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.IntegrationTests;

public sealed class OpenApiDocumentFixture : IAsyncLifetime
{
    private readonly AggregatorApiFactory factory = new();

    public JsonElement Document { get; private set; }

    public async ValueTask InitializeAsync()
    {
        using var client = factory.WithWebHostBuilder(builder => builder.UseSetting("OpenApi:EnableUi", "true")).CreateClient();
        Document = await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative));
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}
