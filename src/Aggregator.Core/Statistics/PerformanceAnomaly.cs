namespace Aggregator.Core.Statistics;

public sealed record PerformanceAnomaly(string Api, double RecentAverageMs, double BaselineAverageMs, long RecentCount);
