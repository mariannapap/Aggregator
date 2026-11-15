using Aggregator.Core.Query;

namespace Aggregator.Core.CityReports;

public sealed record ReportFilters(RegionMatch? Region, HourlyRange? HourlyRange);
