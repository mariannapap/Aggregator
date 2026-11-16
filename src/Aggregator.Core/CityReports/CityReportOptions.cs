using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.CityReports;

public sealed class CityReportOptions
{
    public const string SectionName = "CityReport";

    // Cache keys and upstream URLs carry at most 4 decimals (about 11 m), so more would be silently ignored.
    [Range(0, 4)]
    public int CoordinateDecimals { get; set; } = 2;

    [Range(1, 168)]
    public int HourlyWindowHours { get; set; } = 24;

    [Range(1, 200)]
    public int MaxCityLength { get; set; } = 100;

    [Range(1, 200)]
    public int MaxRegionLength { get; set; } = 100;

    [Range(1, 100)]
    public int DefaultPlaceLimit { get; set; } = 20;

    [Range(1, 100)]
    public int MaxPlaceLimit { get; set; } = 100;

    // Places processed at once by one request; each place makes up to one call per source.
    [Range(1, 100)]
    public int MaxConcurrentPlaces { get; set; } = 5;
}
