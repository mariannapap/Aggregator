using System.Net;

namespace Aggregator.EndToEndTests;

public sealed class TokenExpiryTests
{
    private static readonly TimeSpan PastTokenLifetime = TimeSpan.FromSeconds(13);

    [Fact]
    public async Task Token_WorksUntilItsLifetimeEnds_ThenIsRejectedAsExpired()
    {
        using var session = await ApiSession.LoginAsync(renewToken: false);
        var city = E2EEnvironment.UniqueCity("Token");

        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync($"/city-report/GR/{city}")).StatusCode);
        await Task.Delay(PastTokenLifetime, TestContext.Current.CancellationToken);
        var expired = await session.GetAsync($"/city-report/GR/{city}");

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Contains("expired", expired.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
