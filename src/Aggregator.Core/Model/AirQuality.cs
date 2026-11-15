using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record AirQuality(CurrentAirQuality? Current, IReadOnlyList<HourlyReading> Hourly);
