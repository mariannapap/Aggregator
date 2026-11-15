using Aggregator.Core.Statistics;

namespace Aggregator.Infrastructure.Http;

internal sealed class StatisticsHandler(string api, IStatisticsRecorder recorder, ExternalApiMetrics metrics, TimeProvider timeProvider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            var elapsed = timeProvider.GetElapsedTime(started);
            recorder.Record(api, elapsed);
            metrics.RecordDuration(api, elapsed);
        }
    }
}
