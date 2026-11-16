using Aggregator.Core.Statistics;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Statistics;

[Collection(RunsAlone.Name)]
[Requirement("R11")]
public sealed class StatisticsStoreTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Record_GroupsDurationsIntoBuckets()
    {
        var store = CreateStore();

        store.Record("weather", TimeSpan.FromMilliseconds(50), succeeded: true);
        store.Record("weather", TimeSpan.FromMilliseconds(100), succeeded: true);
        store.Record("weather", TimeSpan.FromMilliseconds(200), succeeded: true);
        store.Record("weather", TimeSpan.FromMilliseconds(350), succeeded: true);

        var snapshot = Assert.Single(store.Snapshot());
        Assert.Equal("weather", snapshot.Api);
        Assert.Equal(4, snapshot.TotalRequests);
        Assert.Equal(175, snapshot.AverageMs);
        Assert.Equal(new BucketSnapshot(1, 50), snapshot.Fast);
        Assert.Equal(new BucketSnapshot(2, 150), snapshot.Average);
        Assert.Equal(new BucketSnapshot(1, 350), snapshot.Slow);
    }

    [Fact]
    public void Snapshot_KeepsApisSeparateAndOrdered()
    {
        var store = CreateStore();
        store.Record("weather", TimeSpan.FromMilliseconds(10), succeeded: true);
        store.Record("airQuality", TimeSpan.FromMilliseconds(10), succeeded: true);

        Assert.Equal(["airQuality", "weather"], store.Snapshot().Select(snapshot => snapshot.Api));
    }

    [Fact]
    public async Task Record_ParallelWriters_LosesNoUpdates()
    {
        var store = CreateStore();
        const int writers = 16;
        const int perWriter = 5_000;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(() =>
        {
            for (var i = 0; i < perWriter; i++)
                store.Record(writer % 2 == 0 ? "weather" : "airQuality", TimeSpan.FromMilliseconds(i % 300), succeeded: true);
        }, TestContext.Current.CancellationToken)));

        var snapshots = store.Snapshot();
        Assert.Equal(writers * perWriter, snapshots.Sum(snapshot => snapshot.TotalRequests));
        Assert.All(snapshots, snapshot =>
            Assert.Equal(snapshot.TotalRequests, snapshot.Fast.Count + snapshot.Average.Count + snapshot.Slow.Count));
        Assert.Equal(writers * perWriter / 2, store.Recent("weather", 1).CurrentMinute.Count);
    }

    [Fact]
    public async Task Snapshot_WhileWritersRun_IsAlwaysInternallyConsistent()
    {
        var store = CreateStore();
        using var stop = new CancellationTokenSource();

        // Dedicated threads, not thread-pool ones: the writers spin until stopped and must not starve other work.
        var writers = Enumerable.Range(0, 8).Select(writer => Task.Factory.StartNew(
            () =>
            {
                var i = 0;
                while (!stop.IsCancellationRequested)
                    store.Record("weather", TimeSpan.FromMilliseconds(i++ % 300), succeeded: true);
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToList();

        long countWhenReadingStarted;
        long countWhenReadingEnded;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (store.Total("weather").Count < 10_000 && DateTime.UtcNow < deadline)
                await Task.Delay(5, TestContext.Current.CancellationToken);
            countWhenReadingStarted = store.Total("weather").Count;

            // Keep reading until the writers have demonstrably advanced; yielding lets them run even on a single CPU.
            var readUntil = DateTime.UtcNow.AddSeconds(10);
            for (var snapshots = 0; snapshots < 2_000 || (store.Total("weather").Count <= countWhenReadingStarted && DateTime.UtcNow < readUntil); snapshots++)
            {
                foreach (var snapshot in store.Snapshot())
                    Assert.Equal(snapshot.TotalRequests, snapshot.Fast.Count + snapshot.Average.Count + snapshot.Slow.Count);
                Thread.Yield();
            }

            countWhenReadingEnded = store.Total("weather").Count;
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(writers);
        }

        Assert.True(countWhenReadingStarted >= 10_000, "Writers never got going.");
        Assert.True(countWhenReadingEnded > countWhenReadingStarted, "Writers were not running while snapshots were taken.");
    }

    [Fact]
    public void Recent_WindowHoldsOnlyTheRequestedCompleteMinutes()
    {
        var store = CreateStore();
        store.Record("weather", TimeSpan.FromMilliseconds(100), succeeded: true);
        time.Advance(TimeSpan.FromMinutes(6));
        store.Record("weather", TimeSpan.FromMilliseconds(300), succeeded: true);
        time.Advance(TimeSpan.FromMinutes(1));

        var recent = store.Recent("weather", 5);

        Assert.Equal(new WindowSample(1, 300), recent.Window);
        Assert.Equal(new WindowSample(0, 0), recent.CurrentMinute);
        Assert.Equal(2, store.Total("weather").Count);
    }

    [Fact]
    public void Recent_TheMinuteInProgress_IsKeptOutOfTheWindow()
    {
        var store = CreateStore();
        store.Record("weather", TimeSpan.FromMilliseconds(100), succeeded: true);
        time.Advance(TimeSpan.FromSeconds(59));
        store.Record("weather", TimeSpan.FromMilliseconds(300), succeeded: true);

        var recent = store.Recent("weather", 5);

        Assert.Equal(new WindowSample(0, 0), recent.Window);
        Assert.Equal(new WindowSample(2, 400), recent.CurrentMinute);
    }

    [Fact]
    public void Recent_UnknownApi_IsEmpty() => Assert.Equal(RecentActivity.None, CreateStore().Recent("missing", 5));

    [Fact]
    public void MonitoredApis_AreListedBeforeTheirFirstCall_WithoutAverages()
    {
        var store = CreateStore(new MonitoredApi("weather"), new MonitoredApi("airQuality"), new MonitoredApi("weather"));

        var snapshots = store.Snapshot();

        Assert.Equal(["airQuality", "weather"], snapshots.Select(snapshot => snapshot.Api));
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal((0L, 0L), (snapshot.TotalRequests, snapshot.FailedRequests));
            Assert.Null(snapshot.AverageMs);
            Assert.Equal(new BucketSnapshot(0, null), snapshot.Fast);
        });
    }

    [Fact]
    [FailureScenario]
    public void Record_FailedAttempts_AreCountedAndStillTimed()
    {
        var store = CreateStore();

        store.Record("weather", TimeSpan.FromMilliseconds(50), succeeded: true);
        store.Record("weather", TimeSpan.FromMilliseconds(2_000), succeeded: false);

        var snapshot = Assert.Single(store.Snapshot());
        Assert.Equal((2L, 1L), (snapshot.TotalRequests, snapshot.FailedRequests));
        Assert.Equal(new BucketSnapshot(1, 2_000), snapshot.Slow);
    }

    private StatisticsStore CreateStore(params MonitoredApi[] monitored) =>
        new(Options.Create(new StatisticsOptions { FastBelowMs = 100, SlowAboveMs = 200, RecentMinutesKept = 10 }), time, monitored);
}
