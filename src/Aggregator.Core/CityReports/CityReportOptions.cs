using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.CityReports;

public sealed class CityReportOptions
{
    public const string SectionName = "CityReport";

    [Range(0, 6)]
    public int CoordinateDecimals { get; set; } = 2;

    [Range(1, 168)]
    public int HourlyWindowHours { get; set; } = 24;

    [Range(1, 200)]
    public int MaxCityLength { get; set; } = 100;

    [Range(1, 200)]
    public int MaxRegionLength { get; set; } = 100;
}
