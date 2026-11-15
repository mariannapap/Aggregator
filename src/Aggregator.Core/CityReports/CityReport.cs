namespace Aggregator.Core.CityReports;

public sealed record CityReport(ReportFilters Filters, IReadOnlyList<PlaceReport> Places);
