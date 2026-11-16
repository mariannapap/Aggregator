using System.ComponentModel;

namespace Aggregator.Core.Model;

// Cached inside SourceResult<T>: must stay immutable for its [ImmutableObject] promise to hold.
[ImmutableObject(true)]
public sealed record Coordinates(double Latitude, double Longitude)
{
    private const double EarthRadiusKm = 6371.0088;

    public Coordinates Round(int decimals) =>
        new(Math.Round(Latitude, decimals, MidpointRounding.AwayFromZero), Math.Round(Longitude, decimals, MidpointRounding.AwayFromZero));

    // Great-circle (haversine) distance.
    public double DistanceKmTo(Coordinates other)
    {
        var deltaLatitude = ToRadians(other.Latitude - Latitude);
        var deltaLongitude = ToRadians(other.Longitude - Longitude);
        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(haversine));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
