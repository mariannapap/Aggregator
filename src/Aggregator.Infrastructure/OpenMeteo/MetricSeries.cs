namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record MetricSeries(string Metric, IReadOnlyList<double?>? Values, string Unit);
