using System.Globalization;
using Aggregator.Core.Model;

namespace Aggregator.Core.Sources;

public sealed record LocationQuery : ISourceQuery
{
    public LocationQuery(Coordinates coordinates, int coordinateDecimals, bool includeCurrent, HourlyWindow? hourly)
    {
        Coordinates = coordinates.Round(coordinateDecimals);
        IncludesCurrent = includeCurrent;
        Hourly = hourly;
    }

    public Coordinates Coordinates { get; }

    public HourlyWindow? Hourly { get; }

    public bool IncludesCurrent { get; }

    public bool IncludesHourly => Hourly is not null;

    public string CacheKey => string.Create(CultureInfo.InvariantCulture,
        $"{Coordinates.Latitude:0.####}|{Coordinates.Longitude:0.####}|current={IncludesCurrent}|hourly={Hourly?.Start:yyyy-MM-ddTHH}|{Hourly?.Hours}");

    // Without the window start, so the last good value outlives the hour it was fetched in; rows outside the current range are filtered out later.
    public string FallbackKey => string.Create(CultureInfo.InvariantCulture,
        $"{Coordinates.Latitude:0.####}|{Coordinates.Longitude:0.####}|current={IncludesCurrent}|hours={Hourly?.Hours}");
}
