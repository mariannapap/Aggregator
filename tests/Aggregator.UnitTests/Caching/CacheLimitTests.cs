using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class CacheLimitTests : IDisposable
{
    private static readonly Weather Old = new(new CurrentWeather(18, 5), []);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider provider;
    private readonly StaleAwareCache cache;
    private int calls;

    public CacheLimitTests()
    {
        provider = FakeClockCache.Create(time);
        cache = new StaleAwareCache(provider.GetRequiredService<HybridCache>(), time, NullLogger<StaleAwareCache>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NoData_IsKeptOnlyForItsShorterLifetime()
    {
        var times = new CacheTimes(TimeSpan.FromHours(24), TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.Zero);

        await GetAsync(Counted(SourceResult.NoData<Weather>()), times);
        time.Advance(TimeSpan.FromMinutes(9));
        await GetAsync(Counted(SourceResult.NoData<Weather>()), times);
        time.Advance(TimeSpan.FromMinutes(2));
        await GetAsync(Counted(SourceResult.NoData<Weather>()), times);

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task NormalLifetime_AppliesToAnswersWithData_OrWithoutAShorterNoDataLifetime(bool hasData, bool hasNoDataLifetime)
    {
        var times = new CacheTimes(TimeSpan.FromHours(24), TimeSpan.FromHours(1), hasNoDataLifetime ? TimeSpan.FromMinutes(10) : null, TimeSpan.Zero);
        var answer = hasData ? SourceResult.Ok(Old) : SourceResult.NoData<Weather>();

        await GetAsync(Counted(answer), times);
        time.Advance(TimeSpan.FromMinutes(11));
        await GetAsync(Counted(answer), times);

        Assert.Equal(1, calls);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task AfterAFailure_TheSourceIsNotCalledDuringTheCooldown_AndTheLastGoodValueIsServed()
    {
        var times = await WithOldValueExpiredAsync();

        var first = await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);
        time.Advance(TimeSpan.FromSeconds(10));
        var duringCooldown = await GetAsync(Counted(SourceResult.Ok(Old)), times);
        time.Advance(TimeSpan.FromSeconds(21));
        var afterCooldown = await GetAsync(Counted(SourceResult.Ok(Old)), times);

        Assert.Equal((SourceStatus.Stale, SourceStatus.Stale, SourceStatus.Ok), (first.Status, duringCooldown.Status, afterCooldown.Status));
        Assert.Equal(2, calls);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task Cooldown_IsNotExtendedByTheRequestsItTurnsAway()
    {
        var times = await WithOldValueExpiredAsync();
        await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);

        for (var request = 0; request < 3; request++)
        {
            time.Advance(TimeSpan.FromSeconds(10));
            await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);
        }

        time.Advance(TimeSpan.FromSeconds(1));
        await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);

        Assert.Equal(2, calls);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task UnexpectedException_AlsoStartsACooldown()
    {
        var times = await WithOldValueExpiredAsync();

        await cache.GetAsync<Weather>("key", "key", _ => { calls++; throw new FormatException("broken payload"); }, times, Ct);
        await GetAsync(Counted(SourceResult.Ok(Old)), times);

        Assert.Equal(1, calls);
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task WithoutACooldown_EveryRequestCallsTheSource()
    {
        var times = new CacheTimes(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), null, TimeSpan.Zero);

        await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);
        await GetAsync(Counted(SourceResult.Unavailable<Weather>()), times);

        Assert.Equal(2, calls);
    }

    public void Dispose() => provider.Dispose();

    private async Task<CacheTimes> WithOldValueExpiredAsync()
    {
        var times = new CacheTimes(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), null, Cooldown);
        await GetAsync(_ => Task.FromResult(SourceResult.Ok(Old)), times);
        time.Advance(TimeSpan.FromMinutes(2));
        return times;
    }

    private Func<CancellationToken, Task<SourceResult<Weather>>> Counted(SourceResult<Weather> answer) => _ =>
    {
        calls++;
        return Task.FromResult(answer);
    };

    private Task<SourceResult<Weather>> GetAsync(Func<CancellationToken, Task<SourceResult<Weather>>> fetch, CacheTimes times) =>
        cache.GetAsync("key", "key", fetch, times, Ct);
}
