using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Core.Statistics;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Eonet;
using Aggregator.Infrastructure.Http;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    // The meter that records the duration of every external API attempt, for telemetry export.
    public const string ExternalApiMeterName = ExternalApiMetrics.MeterName;

    // Spans for cache fetches, which carry the caller's trace into the shared fetch and its HTTP calls.
    public const string CacheActivitySourceName = CacheTelemetry.SourceName;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>().BindConfiguration(CacheOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddMemoryCache();
        services.AddOptions<MemoryCacheOptions>()
            .Configure<IOptions<CacheOptions>>((memory, cacheOptions) => memory.SizeLimit = cacheOptions.Value.SizeLimitMegabytes * 1024L * 1024L);
        services.AddHybridCache();
        services.AddMetrics();
        services.AddSingleton<StaleAwareCache>();
        services.AddSingleton<ICachePolicy<GeocodingQuery>, GeocodingCachePolicy>();
        services.AddSingleton<ICachePolicy<LocationQuery>, LocationCachePolicy>();
        services.AddSingleton<ICachePolicy<AreaQuery>, AreaCachePolicy>();
        services.AddSingleton<ExternalApiMetrics>();
        services.AddOptions<GeocodingOptions>().BindConfiguration(GeocodingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<HazardsOptions>().BindConfiguration(HazardsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<SourceClientOptions>, SourceClientOptionsValidator>();

        services.AddSource<GeocodingQuery, IReadOnlyList<Place>, GeocodingSource>(configuration, SourceNames.Geocoding);
        services.AddSource<LocationQuery, Weather, WeatherSource>(configuration, SourceNames.Weather);
        services.AddSource<LocationQuery, AirQuality, AirQualitySource>(configuration, SourceNames.AirQuality);
        services.AddSource<LocationQuery, Marine, MarineSource>(configuration, SourceNames.Marine);
        services.AddSource<AreaQuery, Hazards, HazardsSource>(configuration, SourceNames.Hazards);

        services.Decorate(typeof(IExternalSource<,>), typeof(CachingSource<,>));
        return services;
    }

    private static void AddSource<TQuery, TResult, TSource>(this IServiceCollection services, IConfiguration configuration, string name)
        where TQuery : ISourceQuery
        where TResult : class
        where TSource : class, IExternalSource<TQuery, TResult>
    {
        var section = $"{SourceClientOptions.SectionPrefix}:{name}";
        services.AddSingleton(new MonitoredApi(name));

        var client = services.AddHttpClient<IExternalSource<TQuery, TResult>, TSource>(name, (provider, http) =>
            http.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SourceClientOptions>>().Get(name).BaseUrl);
        var resilience = client.AddStandardResilienceHandler(configuration.GetSection($"{section}:Resilience"));

        // After the resilience handler, which sets an infinite HttpClient timeout. Its timeouts end with the response headers,
        // so without this the body download had no limit; this one also covers the wait for a limiter permit.
        client.ConfigureHttpClient((provider, http) =>
            http.Timeout = provider.GetRequiredService<IOptionsMonitor<SourceClientOptions>>().Get(name).CallBudget);

        services.AddOptions<SourceClientOptions>(name)
            .BindConfiguration(section)
            .Configure<IOptionsMonitor<HttpStandardResilienceOptions>>((settings, pipelines) =>
                settings.CallBudget = pipelines.Get(resilience.PipelineName).TotalRequestTimeout.Timeout)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        client.AddHttpMessageHandler(provider => new StatisticsHandler(
            name,
            provider.GetRequiredService<IStatisticsRecorder>(),
            provider.GetRequiredService<ExternalApiMetrics>(),
            provider.GetRequiredService<TimeProvider>()));
    }
}
