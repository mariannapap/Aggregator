using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;

namespace Aggregator.Api.Hosting;

internal sealed partial class AnomalyBackgroundService(
    IAnomalyDetector detector,
    IOptions<AnomalyOptions> options,
    TimeProvider timeProvider,
    ILogger<AnomalyBackgroundService> logger) : BackgroundService
{
    // Only the timer loop touches this, one tick at a time.
    private readonly Dictionary<string, AnomalyState> lastStates = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                Analyze(settings);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Logs transitions only: once when an API becomes anomalous, once when it stops being anomalous.
    internal void Analyze(AnomalyOptions settings)
    {
        try
        {
            foreach (var api in detector.Assess())
            {
                var wasAnomalous = lastStates.GetValueOrDefault(api.Api) == AnomalyState.Anomalous;
                lastStates[api.Api] = api.State;

                if (api.State == AnomalyState.Anomalous && !wasAnomalous)
                    LogAnomaly(api.Api, settings.WindowMinutes, api.RecentAverageMs, api.RecentCount, api.BaselineAverageMs, settings.Ratio, settings.MinimumIncreaseMs);
                else if (api.State != AnomalyState.Anomalous && wasAnomalous)
                    LogAnomalyEnded(api.Api, api.State, settings.WindowMinutes, api.RecentAverageMs, api.RecentCount, api.BaselineAverageMs);
            }
        }
        catch (Exception exception)
        {
            LogAnalysisFailed(exception);
        }
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Performance anomaly for {Api}: last {WindowMinutes} complete min average {RecentAverageMs} ms over {RecentCount} requests vs baseline {BaselineAverageMs} ms (threshold x{Ratio} and +{MinimumIncreaseMs} ms)")]
    private partial void LogAnomaly(string api, int windowMinutes, double? recentAverageMs, long recentCount, double? baselineAverageMs, double ratio, double minimumIncreaseMs);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Performance anomaly analysis failed")]
    private partial void LogAnalysisFailed(Exception exception);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information,
        Message = "Performance anomaly for {Api} ended, now {State}: last {WindowMinutes} complete min average {RecentAverageMs} ms over {RecentCount} requests vs baseline {BaselineAverageMs} ms")]
    private partial void LogAnomalyEnded(string api, AnomalyState state, int windowMinutes, double? recentAverageMs, long recentCount, double? baselineAverageMs);
}
