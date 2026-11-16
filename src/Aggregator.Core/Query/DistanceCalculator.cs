using Aggregator.Core.Model;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.Query;

public sealed class DistanceCalculator(IOptions<DistanceOptions> options)
{
    public double DistanceKm(Coordinates coordinates)
    {
        var reference = options.Value;
        return new Coordinates(reference.ReferenceLatitude, reference.ReferenceLongitude).DistanceKmTo(coordinates);
    }
}
