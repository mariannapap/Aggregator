namespace Aggregator.Api.Contracts;

/// <summary>The anomaly rules, set in the Anomaly configuration section.</summary>
/// <param name="WindowMinutes">Complete minutes in the recent window.</param>
/// <param name="Ratio">For anomalous, the recent average must be above the baseline average times this.</param>
/// <param name="MinimumIncreaseMs">For anomalous, the recent average must also be at least this many milliseconds above the baseline average.</param>
/// <param name="MinSamples">Attempts needed in both the recent window and the baseline before a state other than insufficient_data is given.</param>
public sealed record AnomalyRulesResponse(int WindowMinutes, double Ratio, double MinimumIncreaseMs, int MinSamples);
