using Aggregator.Core.Statistics;

namespace Aggregator.Api.Contracts;

internal static class StatisticsMapper
{
    public static StatisticsResponse ToResponse(this IReadOnlyList<ApiStatisticsSnapshot> snapshots, StatisticsOptions options) =>
        new(
            new BucketThresholdsResponse(options.FastBelowMs, options.SlowAboveMs),
            snapshots.ToDictionary(
                snapshot => snapshot.Api,
                snapshot => new ApiStatisticsResponse(
                    snapshot.TotalRequests,
                    snapshot.AverageMs,
                    new PerformanceBucketsResponse(
                        new BucketResponse(snapshot.Fast.Count, snapshot.Fast.AverageMs),
                        new BucketResponse(snapshot.Average.Count, snapshot.Average.AverageMs),
                        new BucketResponse(snapshot.Slow.Count, snapshot.Slow.AverageMs)))));
}
