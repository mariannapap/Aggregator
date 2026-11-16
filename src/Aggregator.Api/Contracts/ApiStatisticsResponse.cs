using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>Calls to one external API. Every HTTP attempt counts, retries included; answers served from the cache do not.</summary>
/// <param name="TotalRequests">HTTP attempts made.</param>
/// <param name="FailedRequests">Attempts that threw or got 5xx, 408 or 429. They are still counted and timed.</param>
/// <param name="AverageMs">Average attempt duration in milliseconds; null before the first attempt.</param>
/// <param name="Buckets">Attempts grouped by duration.</param>
/// <param name="Anomaly">Whether the API is currently slower than usual.</param>
public sealed record ApiStatisticsResponse(
    long TotalRequests,
    long FailedRequests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageMs,
    PerformanceBucketsResponse Buckets,
    AnomalyResponse Anomaly);
