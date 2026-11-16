namespace Aggregator.EndToEndTests;

public sealed class FallbackLifecycleTests
{
    private static readonly TimeSpan PastCacheTtl = TimeSpan.FromMilliseconds(2500);

    [Fact]
    public async Task WeatherGoesDownAndRecovers_FreshThenStaleThenFreshAgain()
    {
        using var wireMock = new WireMockAdmin();
        using var session = await ApiSession.LoginAsync();
        var city = E2EEnvironment.UniqueCity("Fallback");
        try
        {
            var fresh = await session.GetReportPlaceAsync(city);
            Assert.Equal("ok", fresh.SourceStatus("weather"));

            await wireMock.FailAsync("weather");
            await Task.Delay(PastCacheTtl, TestContext.Current.CancellationToken);
            var stale = await session.GetReportPlaceAsync(city);
            Assert.Equal("stale", stale.SourceStatus("weather"));
            Assert.Equal(18.5, stale.GetProperty("weather").GetProperty("temperature").GetDouble());
            Assert.Equal("ok", stale.SourceStatus("airQuality"));

            await wireMock.ResetAsync();
            await Task.Delay(PastCacheTtl, TestContext.Current.CancellationToken);
            var recovered = await session.GetReportPlaceAsync(city);
            Assert.Equal("ok", recovered.SourceStatus("weather"));
        }
        finally
        {
            await wireMock.ResetAsync();
        }
    }
}
