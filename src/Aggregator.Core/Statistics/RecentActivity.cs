namespace Aggregator.Core.Statistics;

// Window: the last N complete minutes. CurrentMinute: the minute in progress, kept apart because it is only partly over.
public sealed record RecentActivity(WindowSample Window, WindowSample CurrentMinute)
{
    public static RecentActivity None { get; } = new(new WindowSample(0, 0), new WindowSample(0, 0));
}
