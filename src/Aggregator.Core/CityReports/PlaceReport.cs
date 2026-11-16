using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

public sealed record PlaceReport(
    Place Place,
    double DistanceKm,
    int MatchRank,
    SourceState Geocoding,
    SourceResult<Weather>? Weather,
    SourceResult<AirQuality>? AirQuality,
    SourceResult<Marine>? Marine,
    SourceResult<Hazards>? Hazards,
    IReadOnlyList<HourlyReading>? Hourly);
