using Aggregator.Api.Hosting;
using Aggregator.Core.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Api;

[Requirement("O2")]
public sealed class AnomalyBackgroundServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    // Generous on purpose: the service runs on the thread pool, which can be slow to respond on a busy machine.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<AnomalyBackgroundService> logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tick_WithAnomaly_LogsWarning()
    {
        using var detector = new StubDetector(_ => [new PerformanceAnomaly("weather", 300, 100, 6)]);
        using var service = CreateService(detector);

        await service.StartAsync(Ct);
        await TickUntilAsync(detector, () => logger.Collector.Count >= 1, "a log line");
        await service.StopAsync(Ct);

        var record = logger.Collector.GetSnapshot()[0];
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(3001, record.Id.Id);
        Assert.Contains("weather", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    [FailureScenario]
    public async Task Tick_DetectorThrows_LogsErrorAndKeepsRunning()
    {
        using var detector = new StubDetector(call => call == 1
            ? throw new InvalidOperationException("boom")
            : [new PerformanceAnomaly("airQuality", 400, 100, 5)]);
        using var service = CreateService(detector);

        await service.StartAsync(Ct);
        await TickUntilAsync(detector, () => logger.Collector.Count >= 2, "two log lines");
        await service.StopAsync(Ct);

        var records = logger.Collector.GetSnapshot();
        Assert.Equal((LogLevel.Error, 3002), (records[0].Level, records[0].Id.Id));
        Assert.Equal((LogLevel.Warning, 3001), (records[1].Level, records[1].Id.Id));
    }

    [Fact]
    public async Task Tick_NoAnomalies_RunsTheDetectorAndLogsNothing()
    {
        using var detector = new StubDetector(_ => []);
        using var service = CreateService(detector);

        await service.StartAsync(Ct);
        await TickUntilAsync(detector, () => detector.Calls >= 3, "three detector runs");
        await service.StopAsync(Ct);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    private AnomalyBackgroundService CreateService(IAnomalyDetector detector) =>
        new(detector, Options.Create(new AnomalyOptions { Interval = Interval }), time, logger);

    // Advances fake time one interval at a time and waits for the detector to signal each run, until done.
    private async Task TickUntilAsync(StubDetector detector, Func<bool> done, string what)
    {
        var deadline = DateTime.UtcNow + Deadline;
        while (!done() && DateTime.UtcNow < deadline)
        {
            time.Advance(Interval);
            await detector.WaitForRunAsync(TimeSpan.FromMilliseconds(200));
        }

        Assert.True(done(), $"Timed out after {Deadline.TotalSeconds:0} s waiting for {what}; the detector ran {detector.Calls} time(s).");
    }

    private sealed class StubDetector(Func<int, IReadOnlyList<PerformanceAnomaly>> detect) : IAnomalyDetector, IDisposable
    {
        private readonly SemaphoreSlim ran = new(0);
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public IReadOnlyList<PerformanceAnomaly> Detect()
        {
            var call = Interlocked.Increment(ref calls);
            try
            {
                return detect(call);
            }
            finally
            {
                ran.Release();
            }
        }

        public Task<bool> WaitForRunAsync(TimeSpan timeout) => ran.WaitAsync(timeout, Ct);

        public void Dispose() => ran.Dispose();
    }
}
