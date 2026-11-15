namespace Aggregator.Api.Contracts;

public sealed record ApiStatisticsResponse(long TotalRequests, double AverageMs, PerformanceBucketsResponse Buckets);
