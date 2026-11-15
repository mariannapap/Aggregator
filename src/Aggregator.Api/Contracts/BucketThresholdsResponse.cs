namespace Aggregator.Api.Contracts;

public sealed record BucketThresholdsResponse(int FastBelowMs, int SlowAboveMs);
