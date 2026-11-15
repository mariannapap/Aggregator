namespace Aggregator.Api.Contracts;

public sealed record StatisticsResponse(BucketThresholdsResponse Thresholds, IReadOnlyDictionary<string, ApiStatisticsResponse> Apis);
