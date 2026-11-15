namespace Aggregator.Core.Statistics;

public sealed record ApiStatisticsSnapshot(
    string Api,
    long TotalRequests,
    double AverageMs,
    BucketSnapshot Fast,
    BucketSnapshot Average,
    BucketSnapshot Slow);
