namespace Aggregator.Core.CityReports;

[Flags]
public enum ReportSections
{
    None = 0,
    Weather = 1,
    AirQuality = 2,
    Hourly = 4,
    Marine = 8
}
