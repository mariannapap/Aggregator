using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aggregator.IntegrationTests;

public static class ApiClient
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static WebApplicationFactory<Program> WithSettings(this AggregatorApiFactory factory, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });

    public static async Task<HttpClient> CreateAuthorizedClientAsync(this WebApplicationFactory<Program> factory, string user = "valid")
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user }, Ct);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        return client;
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
