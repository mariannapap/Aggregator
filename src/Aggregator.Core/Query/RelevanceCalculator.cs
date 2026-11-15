using Aggregator.Core.Model;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.Query;

public sealed class RelevanceCalculator(IOptions<RelevanceOptions> options)
{
    private const double EarthRadiusKm = 6371.0088;

    public double DistanceKm(Coordinates coordinates)
    {
        var reference = options.Value;
        var latitude1 = ToRadians(reference.ReferenceLatitude);
        var latitude2 = ToRadians(coordinates.Latitude);
        var deltaLatitude = ToRadians(coordinates.Latitude - reference.ReferenceLatitude);
        var deltaLongitude = ToRadians(coordinates.Longitude - reference.ReferenceLongitude);

        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(latitude1) * Math.Cos(latitude2) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(haversine));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
