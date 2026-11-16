namespace Aggregator.EndToEndTests;

// Compose settings: 1-minute window, 10 s analysis interval, 3 samples minimum.
// Baseline requests in minute M, slow ones in M + 1; the slow minute counts once it is complete, i.e. during M + 2.
[Trait("Duration", "Long")]
public sealed class AnomalyDetectionTests
{
    private const string Warning = "Performance anomaly for weather:";
    private static readonly TimeSpan SlowResponse = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan AnalysisInterval = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WeatherSlowsDown_StatsShowItAnomalous_AndTheWarningIsLoggedOnce()
    {
        using var wireMock = new WireMockAdmin();
        using var session = await ApiSession.LoginAsync();
        try
        {
            await WaitForNextMinuteAsync();
            for (var request = 0; request < 6; request++)
                await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Baseline"));

            await WaitForNextMinuteAsync();
            var linesBefore = ReadLogLines().Count;
            await wireMock.SetGlobalDelayAsync(SlowResponse);
            for (var request = 0; request < 6; request++)
                await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Slow"));

            var anomalous = await WaitForAsync(async () => await session.AnomalyStateAsync("weather") == "anomalous", TimeSpan.FromSeconds(90));
            var logged = await WaitForAsync(() => Task.FromResult(WarningsSince(linesBefore) > 0), TimeSpan.FromSeconds(30));

            // Several analysis runs later the API is still anomalous, but the warning is not repeated.
            await Task.Delay(AnalysisInterval * 2.5, Ct);

            Assert.True(anomalous, "/stats never reported weather as anomalous within 90 s.");
            Assert.True(logged, $"No '{Warning}' line was logged within 30 s.");
            Assert.Equal(1, WarningsSince(linesBefore));
        }
        finally
        {
            await wireMock.ResetAsync();
        }
    }

    private static async Task WaitForNextMinuteAsync()
    {
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(1).AddSeconds(1);
        await Task.Delay(next - now, Ct);
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        }

        return false;
    }

    private static int WarningsSince(int skipLines) =>
        ReadLogLines().Skip(skipLines).Count(line => line.Contains(Warning, StringComparison.Ordinal));

    private static List<string> ReadLogLines()
    {
        using var stream = new FileStream(E2EEnvironment.ApiLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }
}
