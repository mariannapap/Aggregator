using Aggregator.Core.Statistics;

namespace Aggregator.Api.Contracts;

internal static class StatisticsMapper
{
    public static StatisticsResponse ToResponse(
        this IReadOnlyList<ApiStatisticsSnapshot> snapshots,
        IReadOnlyList<ApiPerformance> performance,
        StatisticsOptions statisticsOptions,
        AnomalyOptions anomalyOptions)
    {
        var performanceByApi = performance.ToDictionary(api => api.Api, StringComparer.Ordinal);
        return new(
            new BucketThresholdsResponse(statisticsOptions.FastBelowMs, statisticsOptions.SlowAboveMs),
            new AnomalyRulesResponse(anomalyOptions.WindowMinutes, anomalyOptions.Ratio, anomalyOptions.MinimumIncreaseMs, anomalyOptions.MinSamples),
            snapshots.ToDictionary(
                snapshot => snapshot.Api,
                snapshot => new ApiStatisticsResponse(
                    snapshot.TotalRequests,
                    snapshot.FailedRequests,
                    snapshot.AverageMs,
                    new PerformanceBucketsResponse(
                        new BucketResponse(snapshot.Fast.Count, snapshot.Fast.AverageMs),
                        new BucketResponse(snapshot.Average.Count, snapshot.Average.AverageMs),
                        new BucketResponse(snapshot.Slow.Count, snapshot.Slow.AverageMs)),
                    ToResponse(performanceByApi.GetValueOrDefault(snapshot.Api)))));
    }

    // An API first recorded between the two reads has no assessment yet.
    private static AnomalyResponse ToResponse(ApiPerformance? performance) =>
        performance is null
            ? new AnomalyResponse(AnomalyStateResponse.InsufficientData, 0, null, 0, null)
            : new AnomalyResponse(
                performance.State switch
                {
                    AnomalyState.Normal => AnomalyStateResponse.Normal,
                    AnomalyState.Anomalous => AnomalyStateResponse.Anomalous,
                    _ => AnomalyStateResponse.InsufficientData
                },
                performance.RecentCount,
                performance.RecentAverageMs,
                performance.BaselineCount,
                performance.BaselineAverageMs);
}
