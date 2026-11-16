using Aggregator.Core.Model;
using Aggregator.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9"), Requirement("R6")]
public sealed class StaleAwareCacheFailureTests : IDisposable
{
    private const int Callers = 20;
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan KeepLastGood = TimeSpan.FromHours(1);
    private static readonly TimeSpan PastFreshness = TimeSpan.FromMinutes(2);
    private static readonly Weather Old = new(new CurrentWeather(18, 5), []);
    private static readonly Weather New = new(new CurrentWeather(25, 9), []);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider provider;
    private readonly StaleAwareCache cache;

    public StaleAwareCacheFailureTests()
    {
        provider = FakeClockCache.Create(time);
        cache = new StaleAwareCache(provider.GetRequiredService<HybridCache>());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task Stampede_WhileTheSourceFails_OneUpstreamCallAndEveryCallerGetsUnavailable()
    {
        var source = new GatedSource(SourceResult.Unavailable<Weather>());

        var results = await RunConcurrentlyAsync(source);

        Assert.Equal(1, source.Calls);
        Assert.All(results, result => Assert.Equal(SourceStatus.Unavailable, result.Status));
    }

    [Fact]
    [FailureScenario]
    public async Task Stampede_WhileTheSourceFailsWithHistory_OneUpstreamCallAndEveryCallerGetsStale()
    {
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Old)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);
        var source = new GatedSource(SourceResult.Unavailable<Weather>());

        var results = await RunConcurrentlyAsync(source);

        Assert.Equal(1, source.Calls);
        Assert.All(results, result =>
        {
            Assert.Equal(SourceStatus.Stale, result.Status);
            Assert.Equal(Old.Current, result.Value!.Current);
        });
    }

    [Fact]
    [FailureScenario]
    public async Task Stampede_OneWaitingCallerCancels_TheOthersStillGetTheValue()
    {
        var source = new GatedSource(SourceResult.Ok(New));
        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var cancelled = cache.GetAsync("key", source.FetchAsync, FreshFor, KeepLastGood, leaving.Token);
        await source.WaitUntilStartedAsync();
        var staying = Enumerable.Range(0, 3).Select(_ => cache.GetAsync("key", source.FetchAsync, FreshFor, KeepLastGood, Ct)).ToList();
        await Task.Delay(50, Ct);
        await leaving.CancelAsync();
        source.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var results = await Task.WhenAll(staying);
        Assert.Equal(1, source.Calls);
        Assert.All(results, result => Assert.Equal(SourceStatus.Ok, result.Status));
    }

    [Fact]
    [FailureScenario]
    public async Task LastGoodPastItsRetention_IsNotServed()
    {
        var shortRetention = TimeSpan.FromMinutes(10);
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Old)), FreshFor, shortRetention, Ct);
        time.Advance(FreshFor + shortRetention + TimeSpan.FromMinutes(1));

        var result = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), FreshFor, shortRetention, Ct);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
    }

    [Fact]
    [FailureScenario]
    public async Task SourceRecovers_FreshValueReplacesStaleAndBecomesTheNewLastGood()
    {
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Old)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);
        var stale = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), FreshFor, KeepLastGood, Ct);

        var recovered = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(New)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);
        var staleAgain = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), FreshFor, KeepLastGood, Ct);

        Assert.Equal((SourceStatus.Stale, Old.Current), (stale.Status, stale.Value!.Current));
        Assert.Equal((SourceStatus.Ok, New.Current), (recovered.Status, recovered.Value!.Current));
        Assert.Equal((SourceStatus.Stale, New.Current), (staleAgain.Status, staleAgain.Value!.Current));
    }

    [Fact]
    [FailureScenario]
    public async Task WhileServingStale_EveryRequestStillTriesTheSource()
    {
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Old)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);
        var calls = 0;
        Task<SourceResult<Weather>> Failing(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(SourceResult.Unavailable<Weather>());
        }

        for (var request = 0; request < 3; request++)
            Assert.Equal(SourceStatus.Stale, (await cache.GetAsync("key", Failing, FreshFor, KeepLastGood, Ct)).Status);

        Assert.Equal(3, calls);
    }

    [Fact]
    [FailureScenario]
    public async Task PartialResult_IsKeptAsLastGoodAndServedStaleWithItsData()
    {
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Partial(Old)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);

        var result = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Unavailable<Weather>()), FreshFor, KeepLastGood, Ct);

        Assert.Equal(SourceStatus.Stale, result.Status);
        Assert.Equal(Old.Current, result.Value!.Current);
    }

    [Fact]
    [FailureScenario]
    public async Task CallerCancelsDuringTheFetch_CancellationPropagatesAndNothingIsCached()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<SourceResult<Weather>> Hanging(CancellationToken token)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return SourceResult.Ok(Old);
        }

        var pending = cache.GetAsync("key", Hanging, FreshFor, KeepLastGood, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var next = await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(New)), FreshFor, KeepLastGood, Ct);
        Assert.Equal((SourceStatus.Ok, New.Current), (next.Status, next.Value!.Current));
    }

    [Fact]
    [FailureScenario]
    public async Task UnexpectedExceptionInTheSource_PropagatesInsteadOfBeingHiddenAsStale()
    {
        await cache.GetAsync("key", _ => Task.FromResult(SourceResult.Ok(Old)), FreshFor, KeepLastGood, Ct);
        time.Advance(PastFreshness);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAsync<Weather>("key", _ => throw new InvalidOperationException("bug"), FreshFor, KeepLastGood, Ct));
    }

    public void Dispose() => provider.Dispose();

    private async Task<SourceResult<Weather>[]> RunConcurrentlyAsync(GatedSource source)
    {
        var callers = Enumerable.Range(0, Callers).Select(_ => cache.GetAsync("key", source.FetchAsync, FreshFor, KeepLastGood, Ct)).ToList();
        await source.WaitUntilStartedAsync();
        await Task.Delay(100, Ct);
        source.Release();
        return await Task.WhenAll(callers);
    }

    private sealed class GatedSource(SourceResult<Weather> result)
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public async Task<SourceResult<Weather>> FetchAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await gate.Task.WaitAsync(cancellationToken);
            return result;
        }

        public Task WaitUntilStartedAsync() => started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        public void Release() => gate.TrySetResult();
    }
}
