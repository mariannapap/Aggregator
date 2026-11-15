namespace Aggregator.Api.Contracts;

public sealed record HourlyReadingResponse(DateTimeOffset Time, string Category, string Metric, double Value, string Unit);
