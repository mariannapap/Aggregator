using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record HourlyReading(DateTimeOffset Time, HourlyCategory Category, string Metric, double Value, string Unit);
