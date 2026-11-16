namespace Aggregator.Core.Statistics;

public interface IStatisticsReader
{
    IReadOnlyList<string> Apis { get; }

    IReadOnlyList<ApiStatisticsSnapshot> Snapshot();

    WindowSample Total(string api);

    RecentActivity Recent(string api, int completeMinutes);
}
