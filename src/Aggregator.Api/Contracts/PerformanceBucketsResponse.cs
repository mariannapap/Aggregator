namespace Aggregator.Api.Contracts;

public sealed record PerformanceBucketsResponse(BucketResponse Fast, BucketResponse Average, BucketResponse Slow);
