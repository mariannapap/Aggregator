using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record CurrentWeather(double Temperature, double WindSpeed);
