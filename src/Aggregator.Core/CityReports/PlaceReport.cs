using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

public sealed record PlaceReport(
    Place Place,
    double DistanceKm,
    SourceStatus GeocodingStatus,
    SourceResult<Weather>? Weather,
    SourceResult<AirQuality>? AirQuality,
    SourceResult<Marine>? Marine,
    IReadOnlyList<HourlyReading>? Hourly);
