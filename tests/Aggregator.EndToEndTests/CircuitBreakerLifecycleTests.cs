namespace Aggregator.EndToEndTests;

public sealed class CircuitBreakerLifecycleTests
{
    private static readonly TimeSpan PastBreakDuration = TimeSpan.FromSeconds(6);

    [Fact]
    public async Task MarineFailures_OpenTheCircuit_ThenHalfOpenProbeClosesItAfterRecovery()
    {
        using var wireMock = new WireMockAdmin();
        using var session = await ApiSession.LoginAsync();
        try
        {
            await wireMock.FailAsync("marine");
            for (var request = 0; request < 4; request++)
                Assert.Equal("unavailable", ApiSession.Status(await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Breaker")), "marine"));

            var callsWhenOpen = await wireMock.CountAsync("marine");
            var whileOpen = await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Breaker"));
            Assert.Equal("unavailable", ApiSession.Status(whileOpen, "marine"));
            Assert.Equal("ok", ApiSession.Status(whileOpen, "weather"));
            Assert.Equal(callsWhenOpen, await wireMock.CountAsync("marine"));

            await wireMock.ResetAsync();
            await Task.Delay(PastBreakDuration, TestContext.Current.CancellationToken);
            var callsAfterRecovery = await wireMock.CountAsync("marine");

            var probe = await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Breaker"));
            Assert.Equal("ok", ApiSession.Status(probe, "marine"));
            var closed = await session.GetReportPlaceAsync(E2EEnvironment.UniqueCity("Breaker"));
            Assert.Equal("ok", ApiSession.Status(closed, "marine"));
            Assert.Equal(callsAfterRecovery + 2, await wireMock.CountAsync("marine"));
        }
        finally
        {
            await wireMock.ResetAsync();
        }
    }
}
