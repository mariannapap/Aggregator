using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record Marine(CurrentMarine? Current, IReadOnlyList<HourlyReading> Hourly);
