using System.Net;
using Aggregator.Core.Statistics;

namespace Aggregator.Infrastructure.Http;

internal sealed class StatisticsHandler(string api, IStatisticsRecorder recorder, ExternalApiMetrics metrics, TimeProvider timeProvider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var succeeded = false;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            succeeded = !IsFailure(response.StatusCode);
            return response;
        }
        finally
        {
            var elapsed = timeProvider.GetElapsedTime(started);
            recorder.Record(api, elapsed, succeeded);
            metrics.RecordDuration(api, elapsed);
        }
    }

    // Open-Meteo answers 400 when it has no data for a request, so only server errors, timeouts and throttling count as failures.
    private static bool IsFailure(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout;
}
