namespace Aggregator.Api.Contracts;

/// <summary>Attempts grouped by duration; see thresholds for the limits.</summary>
/// <param name="Fast">Attempts shorter than thresholds.fastBelowMs.</param>
/// <param name="Average">Attempts from thresholds.fastBelowMs to thresholds.slowAboveMs.</param>
/// <param name="Slow">Attempts longer than thresholds.slowAboveMs.</param>
public sealed record PerformanceBucketsResponse(BucketResponse Fast, BucketResponse Average, BucketResponse Slow);
