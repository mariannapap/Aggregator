namespace Aggregator.Core.Statistics;

public interface IAnomalyDetector
{
    IReadOnlyList<ApiPerformance> Assess();
}
