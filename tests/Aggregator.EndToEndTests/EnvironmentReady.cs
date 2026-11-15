namespace Aggregator.EndToEndTests;

public sealed class EnvironmentReady : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    public async ValueTask InitializeAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        await WaitForAsync(client, new Uri(E2EEnvironment.WireMockUrl, "/__admin/mappings"));
        await WaitForAsync(client, new Uri(E2EEnvironment.ApiUrl, "/health"));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task WaitForAsync(HttpClient client, Uri uri)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(uri)).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"{uri} was not ready within {Timeout}.");
    }
}
