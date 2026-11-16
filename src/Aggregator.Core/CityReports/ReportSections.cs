namespace Aggregator.Core.CityReports;

[Flags]
public enum ReportSections
{
    None = 0,
    Weather = 1,
    AirQuality = 2,
    Marine = 4,
    Hourly = 8
}
