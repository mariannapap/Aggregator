using Aggregator.Api.Hosting;
using Aggregator.Core.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Api;

// Runs are sequential, so once the detector has started run N + 1, everything run N logs is in the collector.
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
    public async Task LastingAnomaly_LogsOneWarning()
    {
        using var detector = new StubDetector(_ => [Weather(AnomalyState.Anomalous)]);

        var records = await RunAsync(detector, runs: 3);

        var record = Assert.Single(records);
        Assert.Equal((LogLevel.Warning, 3001), (record.Level, record.Id.Id));
        Assert.Contains("weather", record.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AnomalyState.Normal)]
    [InlineData(AnomalyState.InsufficientData)]
    public async Task AnomalyEnds_LogsOneInformation(AnomalyState after)
    {
        using var detector = new StubDetector(call => [Weather(call <= 2 ? AnomalyState.Anomalous : after)]);

        var records = await RunAsync(detector, runs: 4);

        Assert.Equal([(LogLevel.Warning, 3001), (LogLevel.Information, 3003)], records.Select(record => (record.Level, record.Id.Id)));
        Assert.Contains(after.ToString(), records[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnomalyReturns_IsLoggedAgain()
    {
        using var detector = new StubDetector(call => [Weather(call == 2 ? AnomalyState.Normal : AnomalyState.Anomalous)]);

        var records = await RunAsync(detector, runs: 3);

        Assert.Equal([3001, 3003, 3001], records.Select(record => record.Id.Id));
    }

    [Fact]
    [FailureScenario]
    public async Task DetectorThrows_LogsErrorAndKeepsRunning()
    {
        using var detector = new StubDetector(call => call == 1
            ? throw new InvalidOperationException("boom")
            : [new ApiPerformance("airQuality", AnomalyState.Anomalous, 5, 400, 50, 100)]);

        var records = await RunAsync(detector, runs: 2);

        Assert.Equal([(LogLevel.Error, 3002), (LogLevel.Warning, 3001)], records.Select(record => (record.Level, record.Id.Id)));
    }

    [Fact]
    public async Task NoAnomalies_RunsTheDetectorAndLogsNothing()
    {
        using var detector = new StubDetector(_ => [Weather(AnomalyState.Normal), Weather(AnomalyState.InsufficientData) with { Api = "marine" }]);

        Assert.Empty(await RunAsync(detector, runs: 3));
    }

    private static ApiPerformance Weather(AnomalyState state) => new("weather", state, 6, 300, 60, 100);

    private async Task<IReadOnlyList<FakeLogRecord>> RunAsync(StubDetector detector, int runs)
    {
        using var service = new AnomalyBackgroundService(detector, Options.Create(new AnomalyOptions { Interval = Interval }), time, logger);
        await service.StartAsync(Ct);

        var deadline = DateTime.UtcNow + Deadline;
        while (detector.Calls <= runs && DateTime.UtcNow < deadline)
        {
            time.Advance(Interval);
            await detector.WaitForRunAsync(TimeSpan.FromMilliseconds(200));
        }

        await service.StopAsync(Ct);
        Assert.True(detector.Calls > runs, $"Timed out after {Deadline.TotalSeconds:0} s; the detector ran {detector.Calls} time(s), {runs + 1} needed.");

        // The extra run only proves the earlier ones finished logging; it repeats the last state, so it logs nothing new.
        return logger.Collector.GetSnapshot();
    }

    private sealed class StubDetector(Func<int, IReadOnlyList<ApiPerformance>> assess) : IAnomalyDetector, IDisposable
    {
        private readonly SemaphoreSlim ran = new(0);
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public IReadOnlyList<ApiPerformance> Assess()
        {
            var call = Interlocked.Increment(ref calls);
            try
            {
                return assess(call);
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
