namespace Aggregator.Api.Contracts;

/// <summary>Calls to the external APIs since the service started. In memory, per instance; reset on restart.</summary>
/// <param name="Thresholds">The bucket limits.</param>
/// <param name="AnomalyRules">The rules behind each API's anomaly state.</param>
/// <param name="Apis">Statistics per external API, keyed by API name; every API is listed, even before its first call.</param>
public sealed record StatisticsResponse(
    BucketThresholdsResponse Thresholds,
    AnomalyRulesResponse AnomalyRules,
    IReadOnlyDictionary<string, ApiStatisticsResponse> Apis);
