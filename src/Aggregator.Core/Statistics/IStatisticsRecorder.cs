namespace Aggregator.Core.Statistics;

public interface IStatisticsRecorder
{
    void Record(string api, TimeSpan duration);
}
