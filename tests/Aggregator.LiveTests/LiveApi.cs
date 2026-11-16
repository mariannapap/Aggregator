using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aggregator.LiveTests;

// The real app with its real HTTP clients: nothing between it and Open-Meteo or NASA EONET is stubbed.
public sealed class LiveApi : WebApplicationFactory<Program>
{
    public const string Switch = "AGGREGATOR_LIVE_TESTS";

    public static bool Enabled => Environment.GetEnvironmentVariable(Switch) == "1";

    public static string SkipReason => $"Calls the real external APIs; set {Switch}=1 to run.";

    public async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user = "valid" }, cancellationToken);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("MockAuth:Enabled", "true");
    }
}
