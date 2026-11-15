using System.ComponentModel;

namespace Aggregator.Core.Model;

[ImmutableObject(true)]
public sealed record Coordinates(double Latitude, double Longitude)
{
    public Coordinates Round(int decimals) =>
        new(Math.Round(Latitude, decimals, MidpointRounding.AwayFromZero), Math.Round(Longitude, decimals, MidpointRounding.AwayFromZero));
}
