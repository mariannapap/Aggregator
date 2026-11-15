using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Core.Statistics;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Http;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHybridCache();
        services.AddMetrics();
        services.AddSingleton<StaleAwareCache>();
        services.AddSingleton<ICachePolicy<GeocodingQuery>, GeocodingCachePolicy>();
        services.AddSingleton<ICachePolicy<LocationQuery>, LocationCachePolicy>();
        services.AddSingleton<ExternalApiMetrics>();
        services.AddOptions<GeocodingOptions>().BindConfiguration(GeocodingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSource<GeocodingQuery, IReadOnlyList<Place>, GeocodingSource>(configuration, SourceNames.Geocoding);
        services.AddSource<LocationQuery, Weather, WeatherSource>(configuration, SourceNames.Weather);
        services.AddSource<LocationQuery, AirQuality, AirQualitySource>(configuration, SourceNames.AirQuality);

        services.Decorate(typeof(IExternalSource<,>), typeof(CachingSource<,>));
        return services;
    }

    private static void AddSource<TQuery, TResult, TSource>(this IServiceCollection services, IConfiguration configuration, string name)
        where TQuery : ISourceQuery
        where TResult : class
        where TSource : class, IExternalSource<TQuery, TResult>
    {
        var section = $"{SourceClientOptions.SectionPrefix}:{name}";
        services.AddOptions<SourceClientOptions>(name).BindConfiguration(section).ValidateDataAnnotations().ValidateOnStart();

        var client = services.AddHttpClient<IExternalSource<TQuery, TResult>, TSource>(name, (provider, http) =>
            http.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SourceClientOptions>>().Get(name).BaseUrl);
        client.AddStandardResilienceHandler(configuration.GetSection($"{section}:Resilience"));
        client.AddHttpMessageHandler(provider => new StatisticsHandler(
            name,
            provider.GetRequiredService<IStatisticsRecorder>(),
            provider.GetRequiredService<ExternalApiMetrics>(),
            provider.GetRequiredService<TimeProvider>()));
    }
}
