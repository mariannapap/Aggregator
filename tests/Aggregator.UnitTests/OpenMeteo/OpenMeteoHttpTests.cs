using Aggregator.Infrastructure.OpenMeteo;
using Microsoft.Extensions.Logging.Testing;

namespace Aggregator.UnitTests.OpenMeteo;

[Requirement("R6")]
public sealed class OpenMeteoHttpTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [FailureScenario]
    public async Task CallerCancels_CancellationPropagatesInsteadOfAFailedOutcome()
    {
        var handler = new HangingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var logger = new FakeLogger();

        var call = OpenMeteoHttp.GetAsync<object>(http, "v1/test", "test", logger, cancellation.Token);
        await handler.Started.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    [FailureScenario]
    public async Task TimeoutNotCausedByTheCaller_IsAFailedOutcome()
    {
        using var http = new HttpClient(new HangingHandler()) { BaseAddress = new Uri("https://api.example.test/"), Timeout = TimeSpan.FromMilliseconds(100) };
        var logger = new FakeLogger();

        var response = await OpenMeteoHttp.GetAsync<object>(http, "v1/test", "test", logger, Ct);

        Assert.Equal(OpenMeteoOutcome.Failed, response.Outcome);
        Assert.Single(logger.Collector.GetSnapshot());
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable: the delay never completes.");
        }
    }
}
