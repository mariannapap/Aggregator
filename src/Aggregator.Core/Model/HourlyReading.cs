using System.ComponentModel;

namespace Aggregator.Core.Model;

// Cached inside SourceResult<T>: must stay immutable for its [ImmutableObject] promise to hold.
[ImmutableObject(true)]
public sealed record HourlyReading(DateTimeOffset Time, HourlyCategory Category, string Metric, double Value, string Unit);
