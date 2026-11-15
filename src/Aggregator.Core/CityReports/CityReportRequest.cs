namespace Aggregator.Core.CityReports;

public sealed record CityReportRequest(
    string City,
    string Country,
    ReportSections Sections,
    string? Region,
    PlaceSort Sort,
    HourlyOptions Hourly);
