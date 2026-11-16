using System.Globalization;
using Aggregator.Core.Model;

namespace Aggregator.Core.Sources;

// The area around a place; how far around it is the adapter's setting, the same for every query.
public sealed record AreaQuery : ISourceQuery
{
    public AreaQuery(Coordinates center, int coordinateDecimals) => Center = center.Round(coordinateDecimals);

    public Coordinates Center { get; }

    public string CacheKey => string.Create(CultureInfo.InvariantCulture, $"{Center.Latitude:0.####}|{Center.Longitude:0.####}");
}
