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

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<AnomalyBackgroundService> logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tick_WithAnomaly_LogsWarning()
    {
        var detector = new StubDetector(() => [new PerformanceAnomaly("weather", 300, 100, 6)]);
        using var service = CreateService(detector);

        await service.StartAsync(Ct);
        await AdvanceUntilLoggedAsync(1);
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
        var calls = 0;
        var detector = new StubDetector(() => ++calls == 1
            ? throw new InvalidOperationException("boom")
            : [new PerformanceAnomaly("airQuality", 400, 100, 5)]);
        using var service = CreateService(detector);

        await service.StartAsync(Ct);
        await AdvanceUntilLoggedAsync(2);
        await service.StopAsync(Ct);

        var records = logger.Collector.GetSnapshot();
        Assert.Equal((LogLevel.Error, 3002), (records[0].Level, records[0].Id.Id));
        Assert.Equal((LogLevel.Warning, 3001), (records[1].Level, records[1].Id.Id));
    }

    [Fact]
    public async Task Tick_NoAnomalies_LogsNothing()
    {
        using var service = CreateService(new StubDetector(() => []));

        await service.StartAsync(Ct);
        for (var tick = 0; tick < 5; tick++)
        {
            time.Advance(Interval);
            await Task.Delay(20, Ct);
        }
        await service.StopAsync(Ct);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    private AnomalyBackgroundService CreateService(IAnomalyDetector detector) =>
        new(detector, Options.Create(new AnomalyOptions { Interval = Interval }), time, logger);

    private async Task AdvanceUntilLoggedAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (logger.Collector.Count < count && DateTime.UtcNow < deadline)
        {
            time.Advance(Interval);
            await Task.Delay(20, Ct);
        }
    }

    private sealed class StubDetector(Func<IReadOnlyList<PerformanceAnomaly>> detect) : IAnomalyDetector
    {
        public IReadOnlyList<PerformanceAnomaly> Detect() => detect();
    }
}
