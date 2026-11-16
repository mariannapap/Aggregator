using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.EndToEndTests;

public sealed class ApiSession : IDisposable
{
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromSeconds(3);

    private readonly HttpClient client = new() { BaseAddress = E2EEnvironment.ApiUrl, Timeout = TimeSpan.FromSeconds(30) };
    private readonly string user;
    private readonly bool renewToken;
    private DateTime tokenExpiresAt;

    private ApiSession(string user, bool renewToken)
    {
        this.user = user;
        this.renewToken = renewToken;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ApiSession> LoginAsync(string user = "valid", bool renewToken = true)
    {
        var session = new ApiSession(user, renewToken);
        await session.LoginAsync();
        return session;
    }

    public async Task<HttpResponseMessage> GetAsync(string path)
    {
        if (renewToken && DateTime.UtcNow >= tokenExpiresAt - RenewBeforeExpiry)
            await LoginAsync();

        return await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
    }

    public async Task<JsonElement> GetReportPlaceAsync(string city)
    {
        var response = await GetAsync($"/city-report/GR/{city}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("places")[0];
    }

    public async Task<long> TotalRequestsAsync(string api)
    {
        var stats = await (await GetAsync("/stats")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        return stats.GetProperty("apis").TryGetProperty(api, out var entry) ? entry.GetProperty("totalRequests").GetInt64() : 0;
    }

    public async Task<string?> AnomalyStateAsync(string api)
    {
        var stats = await (await GetAsync("/stats")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        return stats.GetProperty("apis").TryGetProperty(api, out var entry) ? entry.GetProperty("anomaly").GetProperty("state").GetString() : null;
    }

    public void Dispose() => client.Dispose();

    private async Task LoginAsync()
    {
        var issuedAt = DateTime.UtcNow;
        var login = await client.PostAsJsonAsync(new Uri("/mock-login", UriKind.Relative), new { user }, Ct);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        tokenExpiresAt = issuedAt.AddSeconds(token.GetProperty("expires_in").GetInt32());
    }
}
