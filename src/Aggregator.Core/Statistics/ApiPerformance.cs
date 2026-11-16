namespace Aggregator.Core.Statistics;

// Recent: the anomaly window of complete minutes. Baseline: every earlier request since the service started.
public sealed record ApiPerformance(
    string Api,
    AnomalyState State,
    long RecentCount,
    double? RecentAverageMs,
    long BaselineCount,
    double? BaselineAverageMs);
