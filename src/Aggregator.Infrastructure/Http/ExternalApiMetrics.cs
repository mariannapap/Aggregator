using System.Diagnostics.Metrics;

namespace Aggregator.Infrastructure.Http;

internal sealed class ExternalApiMetrics
{
    public const string MeterName = "Aggregator.ExternalApis";

    private readonly Histogram<double> duration;

    public ExternalApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        duration = meter.CreateHistogram<double>("aggregator.external_api.duration", "ms", "Duration of each HTTP attempt to an external API.");
    }

    public void RecordDuration(string api, TimeSpan elapsed) =>
        duration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("api", api));
}
