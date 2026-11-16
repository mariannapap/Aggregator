using System.Text.Json;
using Aggregator.Infrastructure.OpenMeteo;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.Logging.Testing;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

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
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal((2006, null), (record.Id.Id, record.Exception));
    }

    [Theory]
    [InlineData("open circuit")]
    [InlineData("limiter")]
    [InlineData("attempt timeout")]
    [InlineData("network")]
    [FailureScenario]
    public async Task ExpectedFailure_IsLoggedOnOneLine_WithoutTheStackTrace(string failure)
    {
        Exception exception = failure switch
        {
            "open circuit" => new BrokenCircuitException(),
            "limiter" => new RateLimiterRejectedException(),
            "attempt timeout" => new TimeoutRejectedException(),
            _ => new HttpRequestException("Connection refused")
        };
        using var http = new HttpClient(new ThrowingHandler(exception)) { BaseAddress = new Uri("https://api.example.test/") };
        var logger = new FakeLogger();

        var response = await OpenMeteoHttp.GetAsync<object>(http, "v1/test", "test", logger, Ct);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(OpenMeteoOutcome.Failed, response.Outcome);
        Assert.Equal((2006, null), (record.Id.Id, record.Exception));
        Assert.Contains(exception.GetType().Name, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    [FailureScenario]
    public async Task UnexpectedFailure_KeepsTheException()
    {
        using var http = new HttpClient(FakeHttpHandler.Returning("not json")) { BaseAddress = new Uri("https://api.example.test/") };
        var logger = new FakeLogger();

        await OpenMeteoHttp.GetAsync<object>(http, "v1/test", "test", logger, Ct);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(2001, record.Id.Id);
        Assert.IsType<JsonException>(record.Exception, exactMatch: false);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
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
