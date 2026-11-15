using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Statistics;

[Requirement("R11")]
public sealed class StatisticsStoreTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Record_GroupsDurationsIntoBuckets()
    {
        var store = CreateStore();

        store.Record("weather", TimeSpan.FromMilliseconds(50));
        store.Record("weather", TimeSpan.FromMilliseconds(100));
        store.Record("weather", TimeSpan.FromMilliseconds(200));
        store.Record("weather", TimeSpan.FromMilliseconds(350));

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
        store.Record("weather", TimeSpan.FromMilliseconds(10));
        store.Record("airQuality", TimeSpan.FromMilliseconds(10));

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
                store.Record(writer % 2 == 0 ? "weather" : "airQuality", TimeSpan.FromMilliseconds(i % 300));
        }, TestContext.Current.CancellationToken)));

        var snapshots = store.Snapshot();
        Assert.Equal(writers * perWriter, snapshots.Sum(snapshot => snapshot.TotalRequests));
        Assert.All(snapshots, snapshot =>
            Assert.Equal(snapshot.TotalRequests, snapshot.Fast.Count + snapshot.Average.Count + snapshot.Slow.Count));
        Assert.Equal(writers * perWriter / 2, store.Recent("weather", 1).Count);
    }

    [Fact]
    public async Task Snapshot_WhileWritersRun_IsAlwaysInternallyConsistent()
    {
        var store = CreateStore();
        using var stop = new CancellationTokenSource();

        var writers = Enumerable.Range(0, 8).Select(writer => Task.Run(() =>
        {
            var i = 0;
            while (!stop.IsCancellationRequested)
                store.Record("weather", TimeSpan.FromMilliseconds(i++ % 300));
        }, TestContext.Current.CancellationToken)).ToList();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.Total("weather").Count < 10_000 && DateTime.UtcNow < deadline)
            await Task.Delay(5, TestContext.Current.CancellationToken);
        var countWhenReadingStarted = store.Total("weather").Count;

        for (var snapshots = 0; snapshots < 2_000; snapshots++)
        {
            foreach (var snapshot in store.Snapshot())
                Assert.Equal(snapshot.TotalRequests, snapshot.Fast.Count + snapshot.Average.Count + snapshot.Slow.Count);
        }

        var countWhenReadingEnded = store.Total("weather").Count;
        await stop.CancelAsync();
        await Task.WhenAll(writers);

        Assert.True(countWhenReadingStarted >= 10_000, "Writers never got going.");
        Assert.True(countWhenReadingEnded > countWhenReadingStarted, "Writers were not running while snapshots were taken.");
    }

    [Fact]
    public void Recent_OnlyCountsTheRequestedMinutes()
    {
        var store = CreateStore();
        store.Record("weather", TimeSpan.FromMilliseconds(100));
        time.Advance(TimeSpan.FromMinutes(6));
        store.Record("weather", TimeSpan.FromMilliseconds(300));

        var recent = store.Recent("weather", 5);

        Assert.Equal(1, recent.Count);
        Assert.Equal(300, recent.AverageMs);
        Assert.Equal(2, store.Total("weather").Count);
    }

    [Fact]
    public void Recent_UnknownApi_IsEmpty() => Assert.Equal(0, CreateStore().Recent("missing", 5).Count);

    private StatisticsStore CreateStore() =>
        new(Options.Create(new StatisticsOptions { FastBelowMs = 100, SlowAboveMs = 200, RecentMinutesKept = 10 }), time);
}
