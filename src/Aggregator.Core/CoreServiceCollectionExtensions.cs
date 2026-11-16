using Aggregator.Core.CityReports;
using Aggregator.Core.Query;
using Aggregator.Core.Statistics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aggregator.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddOptions<CityReportOptions>().BindConfiguration(CityReportOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RegionMatchingOptions>().BindConfiguration(RegionMatchingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RelevanceOptions>().BindConfiguration(RelevanceOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<StatisticsOptions>().BindConfiguration(StatisticsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AnomalyOptions>().BindConfiguration(AnomalyOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IValidateOptions<CityReportOptions>, CityReportOptionsValidator>();
        services.AddSingleton<IValidateOptions<StatisticsOptions>, StatisticsOptionsValidator>();
        services.AddSingleton<IValidateOptions<AnomalyOptions>, AnomalyOptionsValidator>();

        services.AddSingleton<RegionMatcher>();
        services.AddSingleton<RelevanceCalculator>();
        services.AddSingleton<StatisticsStore>();
        services.AddSingleton<IStatisticsRecorder>(provider => provider.GetRequiredService<StatisticsStore>());
        services.AddSingleton<IStatisticsReader>(provider => provider.GetRequiredService<StatisticsStore>());
        services.AddSingleton<IAnomalyDetector, AnomalyDetector>();
        services.AddTransient<CityReportService>();
        return services;
    }
}
