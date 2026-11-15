using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace Aggregator.EndToEndTests;

[Trait("Duration", "Long")]
public sealed class SustainedLoadTests
{
    private const int Clients = 20;
    private static readonly TimeSpan P95Budget = TimeSpan.FromSeconds(2);
    private static readonly string[] Cities = [.. Enumerable.Range(0, 300).Select(index => string.Create(CultureInfo.InvariantCulture, $"Load-{index}"))];

    [Fact]
    public async Task TwentyClients_NoErrors_LatencyWithinBudget_StatisticsMatchTheApiSide()
    {
        using var wireMock = new WireMockAdmin();
        using var session = await ApiSession.LoginAsync();
        var statsBefore = await session.TotalRequestsAsync("weather");
        var callsBefore = await wireMock.CountAsync("weather");

        var latencies = new ConcurrentBag<TimeSpan>();
        var statuses = new ConcurrentBag<HttpStatusCode>();
        var deadline = DateTime.UtcNow + E2EEnvironment.LoadDuration;
        var clients = Enumerable.Range(0, Clients).Select(client => Task.Run(async () =>
        {
            using var clientSession = await ApiSession.LoginAsync();
            var request = client;
            while (DateTime.UtcNow < deadline)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await clientSession.GetAsync($"/city-report/GR/{Cities[request++ % Cities.Length]}");
                latencies.Add(stopwatch.Elapsed);
                statuses.Add(response.StatusCode);
            }
        }, TestContext.Current.CancellationToken));
        await Task.WhenAll(clients);

        var sorted = latencies.Order().ToList();
        var p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
        var upstreamCalls = await wireMock.CountAsync("weather") - callsBefore;
        var recordedAttempts = await session.TotalRequestsAsync("weather") - statsBefore;
        TestContext.Current.SendDiagnosticMessage(string.Create(
            CultureInfo.InvariantCulture,
            $"Load: {sorted.Count} requests from {Clients} clients in {E2EEnvironment.LoadDuration.TotalSeconds:0} s ({sorted.Count / E2EEnvironment.LoadDuration.TotalSeconds:0}/s), p50 {sorted[sorted.Count / 2].TotalMilliseconds:0.0} ms, p95 {p95.TotalMilliseconds:0.0} ms, max {sorted[^1].TotalMilliseconds:0} ms; weather API calls {upstreamCalls}, recorded attempts {recordedAttempts}"));

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.True(p95 < P95Budget, $"p95 {p95.TotalMilliseconds:0} ms exceeds {P95Budget.TotalMilliseconds:0} ms.");
        Assert.True(upstreamCalls > Cities.Length, $"Only {upstreamCalls} weather calls: the load never got past the cache.");
        Assert.Equal(upstreamCalls, recordedAttempts);
    }
}
