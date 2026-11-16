using Aggregator.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Aggregator.Api.Composition;

internal static class TelemetryRegistration
{
    public const string EndpointKey = "Telemetry:OtlpEndpoint";

    // OpenTelemetry is wired only when an OTLP endpoint is configured; otherwise nothing is registered and nothing changes.
    public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration[EndpointKey], UriKind.Absolute, out var endpoint))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("aggregator-api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(InfrastructureServiceCollectionExtensions.CacheActivitySourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(InfrastructureServiceCollectionExtensions.ExternalApiMeterName))
            .UseOtlpExporter(OpenTelemetry.Exporter.OtlpExportProtocol.Grpc, endpoint);
        return services;
    }
}
