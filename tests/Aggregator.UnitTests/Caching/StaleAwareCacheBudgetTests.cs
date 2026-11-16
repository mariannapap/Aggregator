using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Caching;

// The budget runs on the fake clock, so it ends exactly when the test advances time past it.
[Collection(RunsAlone.Name)]
[Requirement("R6"), Requirement("R9")]
public sealed class StaleAwareCacheBudgetTests : IDisposable
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);
    private static readonly CacheTimes Times = new(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), null, TimeSpan.FromSeconds(30), Budget);
    private static readonly Weather Old = new(new CurrentWeather(18, 5), []);
    private static readonly Weather New = new(new CurrentWeather(25, 9), []);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<StaleAwareCache> logger = new();
    private readonly ServiceProvider provider;
    private readonly StaleAwareCache cache;
    private readonly GatedSource slow = new(SourceResult.Ok(New));

    public StaleAwareCacheBudgetTests()
    {
        provider = FakeClockCache.Create(time);
        cache = new StaleAwareCache(provider.GetRequiredService<HybridCache>(), time, logger);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task SourceSlowerThanTheBudget_WithHistory_IsStaleWhenTheBudgetEnds()
    {
        await GetAsync(_ => Task.FromResult(SourceResult.Ok(Old)), Times);
        time.Advance(TimeSpan.FromMinutes(2));

        var result = await RunOutOfBudgetAsync(GetAsync(slow.FetchAsync, Times));

        Assert.Equal(SourceStatus.Stale, result.Status);
        Assert.Equal(Old.Current, result.Value!.Current);
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Id.Id == 2102);
    }

    [Fact]
    [FailureScenario]
    public async Task SourceSlowerThanTheBudget_WithoutHistory_IsUnavailable_AndStartsNoCooldown()
    {
        var result = await RunOutOfBudgetAsync(GetAsync(slow.FetchAsync, Times));
        await slow.WaitUntilFinishedAsync();
        var calls = 0;
        var next = await GetAsync(_ => { calls++; return Task.FromResult(SourceResult.Ok(New)); }, Times);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Equal((1, SourceStatus.Ok), (calls, next.Status));
    }

    [Fact]
    public async Task BudgetEndsForOneCaller_TheSharedFetchCarriesOnForTheOthers()
    {
        var patient = GetAsync(slow.FetchAsync, Times with { CallBudget = TimeSpan.FromMinutes(1) });

        var impatient = await RunOutOfBudgetAsync(GetAsync(slow.FetchAsync, Times));
        slow.Release();
        var completed = await patient.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        var cached = await GetAsync(slow.FetchAsync, Times);

        Assert.Equal(SourceStatus.Unavailable, impatient.Status);
        Assert.Equal(SourceStatus.Ok, completed.Status);
        Assert.Equal((1, SourceStatus.Ok), (slow.Calls, cached.Status));
    }

    [Fact]
    [FailureScenario]
    public async Task CallerCancelsWithinTheBudget_CancellationPropagates()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var pending = cache.GetAsync("key", "key", slow.FetchAsync, Times, caller.Token);
        await slow.WaitUntilStartedAsync();

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    public void Dispose()
    {
        slow.Release();
        provider.Dispose();
    }

    private Task<SourceResult<Weather>> GetAsync(Func<CancellationToken, Task<SourceResult<Weather>>> fetch, CacheTimes times) =>
        cache.GetAsync("key", "key", fetch, times, Ct);

    private async Task<SourceResult<Weather>> RunOutOfBudgetAsync(Task<SourceResult<Weather>> pending)
    {
        await slow.WaitUntilStartedAsync();
        Assert.False(pending.IsCompleted);
        time.Advance(Budget);
        return await pending.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    private sealed class GatedSource(SourceResult<Weather> result)
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public async Task<SourceResult<Weather>> FetchAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            try
            {
                await gate.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                finished.TrySetResult();
            }

            return result;
        }

        public Task WaitUntilStartedAsync() => started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        // The cancelled fetch has to be gone before the next call, or that call would join it.
        public Task WaitUntilFinishedAsync() => finished.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        public void Release() => gate.TrySetResult();
    }
}
