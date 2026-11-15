using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;

namespace Aggregator.Api.Hosting;

internal sealed partial class AnomalyBackgroundService(
    IAnomalyDetector detector,
    IOptions<AnomalyOptions> options,
    TimeProvider timeProvider,
    ILogger<AnomalyBackgroundService> logger) : BackgroundService
{
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

    internal void Analyze(AnomalyOptions settings)
    {
        try
        {
            foreach (var anomaly in detector.Detect())
                LogAnomaly(anomaly.Api, settings.WindowMinutes, anomaly.RecentAverageMs, anomaly.BaselineAverageMs, anomaly.RecentCount, settings.Ratio);
        }
        catch (Exception exception)
        {
            LogAnalysisFailed(exception);
        }
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Performance anomaly for {Api}: last {WindowMinutes} min average {RecentAverageMs} ms over {RecentCount} requests vs baseline {BaselineAverageMs} ms (threshold x{Ratio})")]
    private partial void LogAnomaly(string api, int windowMinutes, double recentAverageMs, double baselineAverageMs, long recentCount, double ratio);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Performance anomaly analysis failed")]
    private partial void LogAnalysisFailed(Exception exception);
}
