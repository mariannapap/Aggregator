namespace Aggregator.EndToEndTests;

[Trait("Duration", "Long")]
public sealed class AnomalyDetectionTests
{
    private const string Warning = "Performance anomaly for weather";
    private static readonly TimeSpan SlowResponse = TimeSpan.FromMilliseconds(700);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WeatherSlowsDown_TheBackgroundServiceLogsAnAnomaly()
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

            var logged = await WaitForLogAsync(linesBefore, TimeSpan.FromSeconds(30));

            Assert.True(logged, $"No '{Warning}' line was logged within 30 s.");
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

    private static async Task<bool> WaitForLogAsync(int skipLines, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (ReadLogLines().Skip(skipLines).Any(line => line.Contains(Warning, StringComparison.Ordinal)))
                return true;
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        }

        return false;
    }

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
