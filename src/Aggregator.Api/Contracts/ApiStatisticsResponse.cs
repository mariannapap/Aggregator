using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

public sealed record ApiStatisticsResponse(
    long TotalRequests,
    long FailedRequests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageMs,
    PerformanceBucketsResponse Buckets);
