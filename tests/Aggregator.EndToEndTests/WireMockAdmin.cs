using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.EndToEndTests;

public sealed class WireMockAdmin : IDisposable
{
    private readonly HttpClient client = new() { BaseAddress = E2EEnvironment.WireMockUrl };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task FailAsync(string apiPrefix, int status = 500)
    {
        var mapping = new
        {
            priority = 1,
            request = new { method = "GET", urlPathPattern = $"/{apiPrefix}/.*" },
            response = new { status, body = "{}" }
        };
        (await client.PostAsJsonAsync(new Uri("/__admin/mappings", UriKind.Relative), mapping, Ct)).EnsureSuccessStatusCode();
    }

    public async Task SetGlobalDelayAsync(TimeSpan delay) =>
        (await client.PostAsJsonAsync(new Uri("/__admin/settings", UriKind.Relative), new { fixedDelay = (int)delay.TotalMilliseconds }, Ct)).EnsureSuccessStatusCode();

    /// <summary>Restores the file mappings and removes any delay. WireMock also clears its request journal here.</summary>
    public async Task ResetAsync()
    {
        (await client.PostAsync(new Uri("/__admin/mappings/reset", UriKind.Relative), null, Ct)).EnsureSuccessStatusCode();
        await SetGlobalDelayAsync(TimeSpan.Zero);
    }

    public async Task<int> CountAsync(string apiPrefix)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/__admin/requests/count", UriKind.Relative),
            new { method = "GET", urlPathPattern = $"/{apiPrefix}/.*" },
            Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("count").GetInt32();
    }

    public void Dispose() => client.Dispose();
}
