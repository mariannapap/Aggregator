using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>
/// The API's anomaly state, computed when /stats is called. Recent: the last anomalyRules.windowMinutes complete minutes;
/// the minute in progress is left out. Baseline: every attempt before that, since the service started.
/// </summary>
/// <param name="State">anomalous when the recent average exceeds the baseline by both the ratio and the minimum increase; insufficient_data when either side has fewer than anomalyRules.minSamples attempts.</param>
/// <param name="RecentRequests">Attempts in the recent window.</param>
/// <param name="RecentAverageMs">Their average duration in milliseconds; null when there are none.</param>
/// <param name="BaselineRequests">Attempts in the baseline.</param>
/// <param name="BaselineAverageMs">Their average duration in milliseconds; null when there are none.</param>
public sealed record AnomalyResponse(
    AnomalyStateResponse State,
    long RecentRequests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? RecentAverageMs,
    long BaselineRequests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? BaselineAverageMs);
