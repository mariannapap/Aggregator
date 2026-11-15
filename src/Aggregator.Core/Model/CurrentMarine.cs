using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record CurrentMarine(double WaveHeight, double SeaSurfaceTemperature);
