using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record Weather(CurrentWeather? Current, IReadOnlyList<HourlyReading> Hourly);
