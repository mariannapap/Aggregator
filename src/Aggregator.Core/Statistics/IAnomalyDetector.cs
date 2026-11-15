namespace Aggregator.Core.Statistics;

public interface IAnomalyDetector
{
    IReadOnlyList<PerformanceAnomaly> Detect();
}
