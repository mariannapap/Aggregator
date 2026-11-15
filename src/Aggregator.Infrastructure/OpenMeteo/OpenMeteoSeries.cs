namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record OpenMeteoSeries(string Field, string Metric, string DefaultUnit);
