using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Aggregator.IntegrationTests;

// OpenTelemetry listens process-wide, so these tests also see spans from tests running in parallel:
// each one requests a unique city and only looks at spans in that request's trace.
[Requirement("R11")]
public sealed class TelemetryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutAnEndpoint_NothingIsRegistered()
    {
        await using var factory = new AggregatorApiFactory();

        Assert.Null(factory.Services.GetService<TracerProvider>());
        Assert.Null(factory.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task WithAnEndpoint_RequestsAreTraced_AndMetricsAreCollected()
    {
        var exported = new List<Activity>();
        await using var factory = new AggregatorApiFactory();
        var app = WithTelemetry(factory, exported);
        var path = UniquePath();

        await (await app.CreateAuthorizedClientAsync()).GetJsonAsync(path);

        Assert.NotNull(await RequestSpanAsync(exported, path));
        Assert.NotNull(app.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task UpstreamFetches_StayInTheCallersTrace_DespiteTheSharedCacheFetch()
    {
        var exported = new List<Activity>();
        await using var factory = new AggregatorApiFactory();
        var app = WithTelemetry(factory, exported);
        var path = UniquePath();

        await (await app.CreateAuthorizedClientAsync()).GetJsonAsync($"{path}?include=weather");

        var request = await RequestSpanAsync(exported, path);
        var fetches = Snapshot(exported).Where(span => span.TraceId == request!.TraceId && span.DisplayName.StartsWith("fetch ", StringComparison.Ordinal)).ToList();
        Assert.Equal(["fetch geocoding", "fetch weather"], fetches.Select(span => span.DisplayName).Order(StringComparer.Ordinal));
        Assert.All(fetches, fetch => Assert.Equal(request!.SpanId, fetch.ParentSpanId));
    }

    private static WebApplicationFactory<Program> WithTelemetry(AggregatorApiFactory factory, List<Activity> exported)
    {
        factory.OpenMeteo.GeneratedPlaces = 1;
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Telemetry:OtlpEndpoint", "http://127.0.0.1:4317");
            builder.ConfigureTestServices(services =>
                services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported)));
        });
    }

    private static string UniquePath() => $"/city-report/GR/Telemetry{Guid.NewGuid():N}";

    private static async Task<Activity?> RequestSpanAsync(List<Activity> exported, string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var request = Snapshot(exported).FirstOrDefault(span =>
                span.Kind == ActivityKind.Server && span.GetTagItem("url.path") is string urlPath && urlPath == path);
            if (request is not null)
                return request;
            await Task.Delay(20, Ct);
        }

        Assert.Fail($"No server span for {path}.");
        return null;
    }

    private static List<Activity> Snapshot(List<Activity> exported)
    {
        lock (exported)
            return [.. exported];
    }
}
