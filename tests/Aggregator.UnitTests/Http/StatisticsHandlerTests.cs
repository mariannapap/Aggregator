using System.Diagnostics.Metrics;
using System.Net;
using Aggregator.Core.Statistics;
using Aggregator.Infrastructure.Http;
using Aggregator.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Aggregator.UnitTests.Http;

[Requirement("R11")]
public sealed class StatisticsHandlerTests : IDisposable
{
    private readonly ServiceProvider provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly RecordingRecorder recorder = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Response_IsRecordedAsSucceeded(HttpStatusCode status)
    {
        await SendAsync(new FakeHttpHandler(_ => new HttpResponseMessage(status)));

        Assert.Equal(("weather", true), Assert.Single(recorder.Records));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [Requirement("R6"), FailureScenario]
    public async Task ServerErrorTimeoutOrThrottling_IsRecordedAsFailed(HttpStatusCode status)
    {
        await SendAsync(new FakeHttpHandler(_ => new HttpResponseMessage(status)));

        Assert.Equal(("weather", false), Assert.Single(recorder.Records));
    }

    [Fact]
    [Requirement("R6"), FailureScenario]
    public async Task NetworkFailure_IsRecordedAsFailedAndRethrown()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(new FakeHttpHandler(_ => throw new HttpRequestException("connection refused"))));

        Assert.Equal(("weather", false), Assert.Single(recorder.Records));
    }

    public void Dispose() => provider.Dispose();

    private async Task SendAsync(HttpMessageHandler upstream)
    {
        using var handler = new StatisticsHandler("weather", recorder, new ExternalApiMetrics(provider.GetRequiredService<IMeterFactory>()), TimeProvider.System)
        {
            InnerHandler = upstream
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.example.test/v1/forecast"));
        using var response = await invoker.SendAsync(request, Ct);
    }

    private sealed class RecordingRecorder : IStatisticsRecorder
    {
        public List<(string Api, bool Succeeded)> Records { get; } = [];

        public void Record(string api, TimeSpan duration, bool succeeded) => Records.Add((api, succeeded));
    }
}
