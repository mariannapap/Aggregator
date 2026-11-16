namespace Aggregator.Api.Contracts;

/// <summary>Bucket limits, set by Statistics:FastBelowMs and Statistics:SlowAboveMs.</summary>
/// <param name="FastBelowMs">An attempt shorter than this is fast.</param>
/// <param name="SlowAboveMs">An attempt longer than this is slow; anything in between, limits included, is average.</param>
public sealed record BucketThresholdsResponse(int FastBelowMs, int SlowAboveMs);
